using System.Reflection;
using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static partial class MemberSurfaceAnalyzer
{
    private const TypeAttributes ForwarderAttr = (TypeAttributes)0x00200000;

    internal const string ConservativeMatchingTier = "name+parameter-count";
    internal const string FieldTier = "name";

    public static MemberRefGap[] FindMethodGaps(IReadOnlyList<Inspection> inspections, DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<MemberRefGap> gaps = [];

        foreach (Inspection consumer in inspections)
        {
            if (consumer.View is not { } view)
                continue;

            foreach (MethodRefUse methodRef in view.MethodRefs)
            {
                if (!TryBuildMethodRefGap(methodRef, dependencies, consumer, out MemberRefGap gap))
                    continue;

                gaps.Add(gap);
            }
        }

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ProviderPath,
            item.AssemblyName,
            item.TypeName,
            item.MemberName,
            item.ParameterCount,
            item.MatchingTier))];
    }

    public static TypeRefGap[] FindTypeGaps(IReadOnlyList<Inspection> inspections, DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<TypeRefGap> gaps = [];
        foreach (Inspection consumer in inspections)
        {
            if (consumer.View is not { } view)
                continue;

            foreach (TypeRefUse typeRef in TypeRefs(view))
            {
                if (TryBuildTypeRefGap(typeRef, dependencies, consumer, out TypeRefGap gap))
                    gaps.Add(gap);
            }
        }

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ProviderPath,
            item.AssemblyName,
            item.TypeName))];
    }

    internal static MethodRefUse[] ReadMethodRefs(MetadataReader reader)
    {
        List<MethodRefUse> methodRefs = [];
        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            if (TryReadMethodRef(reader, handle, out MethodRefUse methodRef))
                methodRefs.Add(methodRef);
        }

        return [.. methodRefs];
    }

    internal static MemSurface ReadSurface(MetadataReader reader)
    {
        HashSet<string> typeNames = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<MemberShape>> membersByType = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> fieldsByType = new(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(typeHandle);
            string typeName = TypeName(reader, typeDef.Namespace, typeDef.Name);
            typeNames.Add(typeName);
            HashSet<MemberShape> members = ReadMethods(reader, typeDef);
            membersByType[typeName] = members;
            fieldsByType[typeName] = ReadFields(reader, typeDef);
        }

        AddForwardedTypes(reader, typeNames);
        return new MemSurface(typeNames, membersByType, fieldsByType);
    }

    private static void AddForwardedTypes(MetadataReader reader, HashSet<string> typeNames)
    {
        foreach (ExportedTypeHandle handle in reader.ExportedTypes)
        {
            ExportedType type = reader.GetExportedType(handle);
            if (!type.Attributes.HasFlag(ForwarderAttr))
                continue;

            typeNames.Add(TypeName(reader, type.Namespace, type.Name));
        }
    }

    private static HashSet<MemberShape> ReadMethods(
        MetadataReader reader,
        TypeDefinition typeDef)
    {
        HashSet<MemberShape> members = [];
        foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            string name = reader.GetString(method.Name);
            if (name is ".cctor")
                continue;

            if (TryDecodeParamCount(method, out int paramCount))
                members.Add(new MemberShape(name, paramCount));
        }

        return members;
    }

    private static HashSet<string> ReadFields(
        MetadataReader reader,
        TypeDefinition typeDef)
    {
        HashSet<string> fields = new(StringComparer.Ordinal);
        foreach (FieldDefinitionHandle fieldHandle in typeDef.GetFields())
        {
            FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
            fields.Add(reader.GetString(field.Name));
        }

        return fields;
    }

    private static bool TryReadMethodRef(
        MetadataReader reader,
        MemberReferenceHandle handle,
        out MethodRefUse methodRef)
    {
        methodRef = default;
        MemberReference member = reader.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference)
            return false;

        TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
            return false;

        if (!TryDecodeParamCount(member, out int paramCount))
            return false;

        AssemblyReference assemblyReference = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
        methodRef = new MethodRefUse(
            reader.GetString(assemblyReference.Name),
            TypeName(reader, typeRef.Namespace, typeRef.Name),
            reader.GetString(member.Name),
            paramCount);
        return true;
    }

    private static bool TryBuildMethodRefGap(
        MethodRefUse methodRef,
        DependencyIndex dependencies,
        Inspection consumer,
        out MemberRefGap gap)
    {
        gap = default;
        if (dependencies.ClassifyProvided(methodRef.AssemblyName) != ProvidedKind.None)
            return false;

        if (!dependencies.TryGetProvider(methodRef.AssemblyName, out Inspection provider))
            return false;

        if (provider.View is not { } providerView)
            return false;

        if (!providerView.MemSurface.ContainsType(methodRef.TypeName))
            return false;

        if (!providerView.MemSurface.TryGetMembers(methodRef.TypeName, out HashSet<MemberShape> members))
            return false;

        if (members.Contains(new MemberShape(methodRef.MemberName, methodRef.ParameterCount)))
            return false;

        gap = new MemberRefGap(
            methodRef.AssemblyName,
            methodRef.TypeName,
            methodRef.MemberName,
            methodRef.ParameterCount,
            ConservativeMatchingTier,
            consumer.Path,
            provider.Path);
        return true;
    }

    private static bool TryBuildTypeRefGap(
        TypeRefUse typeRef,
        DependencyIndex dependencies,
        Inspection consumer,
        out TypeRefGap gap)
    {
        gap = default;
        if (dependencies.ClassifyProvided(typeRef.AssemblyName) != ProvidedKind.None)
            return false;

        if (!dependencies.TryGetProvider(typeRef.AssemblyName, out Inspection provider))
            return false;

        if (provider.View is not { } providerView)
            return false;

        if (providerView.MemSurface.ContainsType(typeRef.TypeName))
            return false;

        gap = new TypeRefGap(
            typeRef.AssemblyName,
            typeRef.TypeName,
            consumer.Path,
            provider.Path);
        return true;
    }

    private static bool TryDecodeParamCount(MemberReference member, out int paramCount)
    {
        paramCount = 0;
        if (member.GetKind() != MemberReferenceKind.Method)
            return false;

        return TryDecodeParamCount(
            () => member.DecodeMethodSignature(new ParamCounter(), null),
            out paramCount);
    }

    private static bool TryDecodeParamCount(MethodDefinition method, out int paramCount)
    {
        return TryDecodeParamCount(
            () => method.DecodeSignature(new ParamCounter(), null),
            out paramCount);
    }

    private static bool TryDecodeParamCount(Func<MethodSignature<int>> decode, out int paramCount)
    {
        paramCount = 0;
        try
        {
            MethodSignature<int> signature = decode();
            paramCount = signature.ParameterTypes.Length;
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string TypeName(MetadataReader reader, StringHandle ns, StringHandle name)
    {
        string nsValue = reader.GetString(ns);
        string typeName = reader.GetString(name);
        return nsValue.Length == 0 ? typeName : $"{nsValue}.{typeName}";
    }

}
