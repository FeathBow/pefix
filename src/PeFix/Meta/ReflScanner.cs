using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

internal static partial class ReflScanner
{
    private const string AssemblyType = "System.Reflection.Assembly";
    private const string TypeType = "System.Type";
    private const string ActivatorType = "System.Activator";
    private const string AccessToolsType = "HarmonyLib.AccessTools";
    private const string AppDomainType = "System.AppDomain";
    private const string AssemblyLoadContextType = "System.Runtime.Loader.AssemblyLoadContext";

    public static ReflScan Scan(
        IReadOnlyList<Inspection> inspections,
        HostProfile hostProfile)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(hostProfile);

        var dependencies = DependencyIndex.Build(inspections, hostProfile);
        var references = new List<ReflRef>();
        bool hasResolver = false;
        int desyncCount = 0;

        foreach (Inspection inspection in inspections)
            ScanInspection(inspection, dependencies, references, ref hasResolver, ref desyncCount);

        return new ReflScan(
            [.. references.DistinctBy(DistinctKey)],
            hasResolver,
            desyncCount);
    }

    internal static ReflScan Read(PeRead pe)
    {
        var references = new List<ReflRef>();
        bool hasResolver = false;
        int desyncCount = 0;

        foreach (MethodDefinitionHandle handle in pe.Metadata.MethodDefinitions)
        {
            ReadMethod(
                pe.Path,
                references,
                ref hasResolver,
                ref desyncCount,
                pe.Metadata,
                pe.Reader,
                handle);
        }

        return new ReflScan(
            [.. references.DistinctBy(DistinctKey)],
            hasResolver,
            desyncCount);
    }

    private static void ScanInspection(
        Inspection inspection,
        DependencyIndex dependencies,
        List<ReflRef> references,
        ref bool hasResolver,
        ref int desyncCount)
    {
        if (inspection.View is not { } view)
            return;

        ReflScan reflection = view.Reflection;
        hasResolver |= reflection.HasCustomResolver;
        desyncCount += reflection.DesyncMethodCount;
        if (!ShouldReportReferences(inspection, dependencies))
            return;

        foreach (ReflRef reference in reflection.References)
            if (!IsProvided(reference.ReferenceName, dependencies))
                references.Add(reference);
    }

    private static void ReadMethod(
        string consumerPath,
        List<ReflRef> references,
        ref bool hasResolver,
        ref int desyncCount,
        MetadataReader reader,
        PEReader peReader,
        MethodDefinitionHandle handle)
    {
        MethodDefinition method = reader.GetMethodDefinition(handle);
        if (!TryReadInstructions(peReader, method, out IlInstr[] instructions))
        {
            desyncCount++;
            return;
        }

        hasResolver |= ContainsResolverRegistration(reader, instructions);
        bool staticCtor = reader.GetString(method.Name) == ".cctor";
        AddReflectionReferences(consumerPath, references, reader, instructions, staticCtor);
    }

    private static bool ShouldReportReferences(
        Inspection inspection,
        DependencyIndex dependencies)
    {
        AssemblyIdentity? identity = inspection.AssemblyDefinition;
        return identity is null
            || dependencies.ClassifyProvided(identity.Value.Name) == ProvidedKind.None;
    }

    private static bool TryReadInstructions(
        PEReader peReader,
        MethodDefinition method,
        out IlInstr[] instructions)
    {
        instructions = [];
        if (method.RelativeVirtualAddress == 0)
            return true;

        try
        {
            ImmutableArray<byte> il = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILContent();
            DecodeResult result = IlDecoder.Decode(il);
            instructions = result.Instructions;
            return !result.Desynced;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static void AddReflectionReferences(
        string consumerPath,
        List<ReflRef> references,
        MetadataReader reader,
        IlInstr[] instructions,
        bool staticCtor)
    {
        for (int index = 0; index + 1 < instructions.Length; index++)
        {
            if (instructions[index].OpCode != IlDecoder.Ldstr || !IsCall(instructions[index + 1]))
                continue;

            if (!TryReadUserString(reader, instructions[index].Operand, out string literal))
                continue;

            if (!TryCreateReference(
                new RefReq(reader, instructions[index + 1].Operand, consumerPath),
                literal,
                staticCtor,
                out ReflRef reference))
                continue;

            references.Add(reference);
        }
    }

    private static bool ContainsResolverRegistration(
        MetadataReader reader,
        IlInstr[] instructions)
    {
        return instructions.Any(item =>
            IsCall(item)
            && TryReadMethodTarget(reader, item.Operand, out MethodTarget target)
            && IsResolverRegistration(target));
    }

    private static bool TryCreateReference(
        RefReq req,
        string literal,
        bool staticCtor,
        out ReflRef reference)
    {
        reference = default;
        if (!TryReadMethodTarget(req.Reader, req.MethodToken, out MethodTarget target))
            return false;

        if (!TryParseSinkReference(target, literal, out string referenceName, out bool advisoryOnly))
            return false;

        reference = new ReflRef(
            req.ConsumerPath,
            referenceName,
            target.TypeName,
            target.MethodName,
            advisoryOnly,
            staticCtor);
        return true;
    }

    private static bool TryParseSinkReference(
        MethodTarget target,
        string literal,
        out string referenceName,
        out bool advisoryOnly)
    {
        referenceName = string.Empty;
        advisoryOnly = false;
        if (IsAssemblySink(target))
            return AsmNameParse.TryParse(literal, requireComma: false, out referenceName);

        if (IsTypeSink(target))
            return AsmNameParse.TryParse(literal, requireComma: true, out referenceName);

        if (IsActivatorSink(target))
            return AsmNameParse.TryParse(literal, requireComma: true, out referenceName);

        advisoryOnly = IsHarmonySink(target);
        return advisoryOnly && AsmNameParse.TryParse(literal, requireComma: true, out referenceName);
    }

    private static bool IsAssemblySink(MethodTarget target)
    {
        return target.TypeName == AssemblyType
            && target.MethodName is "Load" or "LoadFrom" or "LoadFile";
    }

    private static bool IsTypeSink(MethodTarget target)
    {
        return target.TypeName == TypeType && target.MethodName == "GetType";
    }

    private static bool IsActivatorSink(MethodTarget target)
    {
        return target.TypeName == ActivatorType && target.MethodName == "CreateInstance";
    }

    private static bool IsHarmonySink(MethodTarget target)
    {
        return target.TypeName == AccessToolsType
            && (target.MethodName == "TypeByName"
                || target.MethodName.Contains("Method", StringComparison.Ordinal));
    }

    private static bool IsResolverRegistration(MethodTarget target)
    {
        return (target.TypeName == AppDomainType && target.MethodName == "add_AssemblyResolve")
            || (target.TypeName == AssemblyLoadContextType && target.MethodName == "add_Resolving");
    }

    private static bool IsProvided(string referenceName, DependencyIndex dependencies)
    {
        return dependencies.TryGetProvider(referenceName, out _)
            || dependencies.ClassifyProvided(referenceName) != ProvidedKind.None;
    }

    private static bool IsCall(IlInstr instruction)
    {
        return instruction.OpCode is IlDecoder.Call or IlDecoder.Callvirt;
    }

    private static object DistinctKey(ReflRef reference)
    {
        return (
            reference.ConsumerPath,
            reference.ReferenceName,
            reference.SinkType,
            reference.SinkMethod,
            reference.AdvisoryOnly,
            reference.StaticCtor);
    }

    private readonly record struct MethodTarget(string TypeName, string MethodName);

    private readonly record struct RefReq(
        MetadataReader Reader,
        int MethodToken,
        string ConsumerPath);
}

internal readonly record struct ReflScan(
    ReflRef[] References,
    bool HasCustomResolver,
    int DesyncMethodCount);

internal readonly record struct ReflRef(
    string ConsumerPath,
    string ReferenceName,
    string SinkType,
    string SinkMethod,
    bool AdvisoryOnly,
    bool StaticCtor);
