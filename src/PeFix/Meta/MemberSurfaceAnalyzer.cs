using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

internal static class MemberSurfaceAnalyzer
{
    public static MemberRefGap[] FindMethodGaps(IReadOnlyList<Inspection> inspections, DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<MemberRefGap> gaps = [];
        var surfaces = new SurfaceCache();

        foreach (Inspection consumer in inspections)
        {
            if (!TryOpenReader(consumer.Path, out MetadataReaderProvider provider))
                continue;

            using MetadataReaderProvider metadataProvider = provider;
            MetadataReader reader = metadataProvider.GetMetadataReader();
            foreach (MemberReferenceHandle handle in reader.MemberReferences)
            {
                if (!TryBuildMethodRefGap(reader, handle, dependencies, consumer, surfaces, out MemberRefGap gap))
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

    private static bool TryBuildMethodRefGap(
        MetadataReader reader,
        MemberReferenceHandle handle,
        DependencyIndex dependencies,
        Inspection consumer,
        SurfaceCache surfaces,
        out MemberRefGap gap)
    {
        gap = default;
        MemberReference member = reader.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference)
            return false;

        TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
            return false;

        AssemblyReference assemblyReference = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
        string referenceName = reader.GetString(assemblyReference.Name);
        if (dependencies.ClassifyProvided(referenceName) != ProvidedKind.None)
            return false;

        if (!dependencies.TryGetProvider(referenceName, out Inspection provider))
            return false;

        string typeName = TypeName(reader, typeRef.Namespace, typeRef.Name);
        if (!TryDecodeParamCount(member, out int paramCount))
            return false;

        if (!surfaces.TryGet(provider, out MemberSurface surface))
            return false;

        string memberName = reader.GetString(member.Name);
        if (!surface.TryGetMembers(typeName, out HashSet<MemberShape> members))
            return false;

        if (members.Contains(new MemberShape(memberName, paramCount)))
            return false;

        gap = new MemberRefGap(
            referenceName,
            typeName,
            memberName,
            paramCount,
            "name+parameter-count",
            consumer.Path,
            provider.Path);
        return true;
    }

    private static MemberSurface? ReadMemberSurface(Inspection provider)
    {
        if (!TryOpenReader(provider.Path, out MetadataReaderProvider readerProvider))
            return null;

        using MetadataReaderProvider metadataProvider = readerProvider;
        MetadataReader reader = metadataProvider.GetMetadataReader();
        Dictionary<string, HashSet<MemberShape>> membersByType = new(StringComparer.Ordinal);

        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(typeHandle);
            string typeName = TypeName(reader, typeDef.Namespace, typeDef.Name);
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

            membersByType[typeName] = members;
        }

        return new MemberSurface(membersByType);
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

    private static bool TryOpenReader(string path, out MetadataReaderProvider provider)
    {
        provider = null!;
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return false;

            PEMemoryBlock metadata = peReader.GetMetadata();
            provider = MetadataReaderProvider.FromMetadataImage(metadata.GetContent());
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (BadImageFormatException)
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

    private sealed class ParamCounter : ISignatureTypeProvider<int, object?>
    {
        public int GetArrayType(int elementType, ArrayShape shape) => elementType;
        public int GetByReferenceType(int elementType) => elementType;
        public int GetFunctionPointerType(MethodSignature<int> signature) => signature.ParameterTypes.Length;
        public int GetGenericInstantiation(int genericType, ImmutableArray<int> typeArguments) => genericType;
        public int GetGenericMethodParameter(object? genericContext, int index) => index;
        public int GetGenericTypeParameter(object? genericContext, int index) => index;
        public int GetModifiedType(int modifierType, int unmodifiedType, bool isRequired) => unmodifiedType;
        public int GetPinnedType(int elementType) => elementType;
        public int GetPointerType(int elementType) => elementType;
        public int GetPrimitiveType(PrimitiveTypeCode typeCode) => 0;
        public int GetSZArrayType(int elementType) => elementType;
        public int GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => 0;
        public int GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => 0;
        public int GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => 0;
    }

    private readonly record struct MemberShape(string Name, int ParameterCount);

    private sealed class MemberSurface(Dictionary<string, HashSet<MemberShape>> membersByType)
    {
        public bool TryGetMembers(string typeName, out HashSet<MemberShape> members)
        {
            return membersByType.TryGetValue(typeName, out members!);
        }
    }

    private sealed class SurfaceCache
    {
        private readonly Dictionary<string, MemberSurface?> byPath = [];

        public bool TryGet(Inspection provider, out MemberSurface surface)
        {
            if (!byPath.TryGetValue(provider.Path, out MemberSurface? cached))
            {
                cached = ReadMemberSurface(provider);
                byPath[provider.Path] = cached;
            }

            if (cached is null)
            {
                surface = null!;
                return false;
            }

            surface = cached;
            return true;
        }
    }
}
