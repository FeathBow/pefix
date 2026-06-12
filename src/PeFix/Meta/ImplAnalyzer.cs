using System.Reflection;
using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static class ImplAnalyzer
{
    private static readonly HashSet<MemberShape> ObjectShapes =
    [
        new MemberShape("ToString", 0),
        new MemberShape("Equals", 1),
        new MemberShape("GetHashCode", 0),
        new MemberShape("GetType", 0),
        new MemberShape("Finalize", 0),
        new MemberShape("MemberwiseClone", 0)
    ];

    private static readonly HashSet<string> TerminalBases = new(StringComparer.Ordinal)
    {
        "System.Object",
        "System.ValueType",
        "System.Enum"
    };

    public static ImplGap[] FindImplGaps(
        IReadOnlyList<Inspection> inspections,
        DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<ImplGap> gaps = [];
        foreach (Inspection consumer in inspections)
        {
            if (consumer.View is not { } view)
                continue;

            foreach (ImplUse use in view.ImplUses)
                AddUseGaps(gaps, use, dependencies, consumer);
        }

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ProviderPath,
            item.AssemblyName,
            item.InterfaceName,
            item.ClassName,
            item.MemberName,
            item.ParameterCount))];
    }

    internal static ImplUse[] ReadImplUses(MetadataReader reader)
    {
        List<ImplUse> uses = [];
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(typeHandle);
            if (!IsConcrete(typeDef))
                continue;

            IfaceRef[] interfaces = ReadInterfaces(reader, typeDef);
            if (interfaces.Length == 0)
                continue;

            if (!TryCollectShapes(reader, typeDef, out HashSet<MemberShape> shapes, out HashSet<string> explicitKeys))
                continue;

            string className = MemberSurfaceAnalyzer.ReadTypeName(reader, typeDef.Namespace, typeDef.Name);
            uses.Add(new ImplUse(className, shapes, explicitKeys, interfaces));
        }

        return [.. uses];
    }

    internal static string ExplicitKey(string assemblyName, string interfaceName, MemberShape shape)
    {
        return $"{assemblyName}|{DimKey(interfaceName, shape)}";
    }

    internal static string DimKey(string interfaceName, MemberShape shape)
    {
        return $"{interfaceName}|{shape.Name}|{shape.ParameterCount}";
    }

    private static void AddUseGaps(
        List<ImplGap> gaps,
        ImplUse use,
        DependencyIndex dependencies,
        Inspection consumer)
    {
        HashSet<string> dimCovered = CollectDimKeys(use, dependencies);
        foreach (IfaceRef iface in use.Interfaces)
        {
            if (!TryGetIface(iface, dependencies, out Inspection provider, out IfaceSurface surface))
                continue;

            foreach (MemberShape shape in surface.AbstractShapes)
            {
                if (IsSatisfied(use, iface, shape, dimCovered))
                    continue;

                gaps.Add(new ImplGap(
                    iface.AssemblyName,
                    iface.InterfaceName,
                    use.ClassName,
                    shape.Name,
                    shape.ParameterCount,
                    consumer.Path,
                    provider.Path));
            }
        }
    }

    private static bool IsSatisfied(
        ImplUse use,
        IfaceRef iface,
        MemberShape shape,
        HashSet<string> dimCovered)
    {
        return use.ClassShapes.Contains(shape)
            || ObjectShapes.Contains(shape)
            || use.ExplicitKeys.Contains(ExplicitKey(iface.AssemblyName, iface.InterfaceName, shape))
            || dimCovered.Contains(DimKey(iface.InterfaceName, shape));
    }

    private static HashSet<string> CollectDimKeys(ImplUse use, DependencyIndex dependencies)
    {
        HashSet<string> covered = new(StringComparer.Ordinal);
        foreach (IfaceRef iface in use.Interfaces)
        {
            if (TryGetIface(iface, dependencies, out _, out IfaceSurface surface))
                covered.UnionWith(surface.OverrideKeys);
        }

        return covered;
    }

    private static bool TryGetIface(
        IfaceRef iface,
        DependencyIndex dependencies,
        out Inspection provider,
        out IfaceSurface surface)
    {
        surface = null!;
        provider = default!;
        if (dependencies.ClassifyProvided(iface.AssemblyName) != ProvidedKind.None)
            return false;

        if (!dependencies.TryGetProvider(iface.AssemblyName, out provider))
            return false;

        if (provider.View is not { } providerView)
            return false;

        return providerView.MemSurface.TryGetIface(iface.InterfaceName, out surface);
    }

    private static bool IsConcrete(TypeDefinition typeDef)
    {
        TypeAttributes attrs = typeDef.Attributes;
        return (attrs & TypeAttributes.ClassSemanticsMask) != TypeAttributes.Interface
            && (attrs & TypeAttributes.Abstract) == 0;
    }

    private static IfaceRef[] ReadInterfaces(MetadataReader reader, TypeDefinition typeDef)
    {
        List<IfaceRef> interfaces = [];
        foreach (InterfaceImplementationHandle handle in typeDef.GetInterfaceImplementations())
        {
            InterfaceImplementation impl = reader.GetInterfaceImplementation(handle);
            if (impl.Interface.Kind != HandleKind.TypeReference)
                continue;

            TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)impl.Interface);
            if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
                continue;

            AssemblyReference assemblyRef = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
            interfaces.Add(new IfaceRef(
                reader.GetString(assemblyRef.Name),
                MemberSurfaceAnalyzer.ReadTypeName(reader, typeRef.Namespace, typeRef.Name)));
        }

        return [.. interfaces];
    }

    private static bool TryCollectShapes(
        MetadataReader reader,
        TypeDefinition typeDef,
        out HashSet<MemberShape> shapes,
        out HashSet<string> explicitKeys)
    {
        shapes = MemberSurfaceAnalyzer.ReadMethods(reader, typeDef);
        explicitKeys = ReadExplicitKeys(reader, typeDef);
        EntityHandle baseType = typeDef.BaseType;
        while (!baseType.IsNil)
        {
            if (baseType.Kind == HandleKind.TypeReference)
            {
                TypeReference baseRef = reader.GetTypeReference((TypeReferenceHandle)baseType);
                string baseName = MemberSurfaceAnalyzer.ReadTypeName(reader, baseRef.Namespace, baseRef.Name);
                return TerminalBases.Contains(baseName);
            }

            if (baseType.Kind != HandleKind.TypeDefinition)
                return false;

            TypeDefinition baseDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
            shapes.UnionWith(MemberSurfaceAnalyzer.ReadMethods(reader, baseDef));
            explicitKeys.UnionWith(ReadExplicitKeys(reader, baseDef));
            baseType = baseDef.BaseType;
        }

        return true;
    }

    internal static HashSet<string> ReadExplicitKeys(MetadataReader reader, TypeDefinition typeDef)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (MethodImplementationHandle handle in typeDef.GetMethodImplementations())
        {
            MethodImplementation impl = reader.GetMethodImplementation(handle);
            if (TryReadDeclKey(reader, impl.MethodDeclaration, out string assemblyName, out string dimKey))
                keys.Add(assemblyName.Length == 0 ? dimKey : $"{assemblyName}|{dimKey}");
        }

        return keys;
    }

    internal static bool TryReadDeclKey(
        MetadataReader reader,
        EntityHandle declaration,
        out string assemblyName,
        out string dimKey)
    {
        assemblyName = string.Empty;
        dimKey = string.Empty;
        return declaration.Kind switch
        {
            HandleKind.MemberReference => TryReadMemberRefKey(reader, (MemberReferenceHandle)declaration, out assemblyName, out dimKey),
            HandleKind.MethodDefinition => TryReadMethodDefKey(reader, (MethodDefinitionHandle)declaration, out dimKey),
            _ => false
        };
    }

    private static bool TryReadMemberRefKey(
        MetadataReader reader,
        MemberReferenceHandle handle,
        out string assemblyName,
        out string dimKey)
    {
        assemblyName = string.Empty;
        dimKey = string.Empty;
        MemberReference member = reader.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference)
            return false;

        TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
            return false;

        if (!MemberSurfaceAnalyzer.TryDecodeParamCount(member, out int paramCount))
            return false;

        AssemblyReference assemblyRef = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
        assemblyName = reader.GetString(assemblyRef.Name);
        string interfaceName = MemberSurfaceAnalyzer.ReadTypeName(reader, typeRef.Namespace, typeRef.Name);
        dimKey = DimKey(interfaceName, new MemberShape(reader.GetString(member.Name), paramCount));
        return true;
    }

    private static bool TryReadMethodDefKey(
        MetadataReader reader,
        MethodDefinitionHandle handle,
        out string dimKey)
    {
        dimKey = string.Empty;
        MethodDefinition method = reader.GetMethodDefinition(handle);
        if (!MemberSurfaceAnalyzer.TryDecodeParamCount(method, out int paramCount))
            return false;

        TypeDefinition declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
        string interfaceName = MemberSurfaceAnalyzer.ReadTypeName(reader, declaringType.Namespace, declaringType.Name);
        dimKey = DimKey(interfaceName, new MemberShape(reader.GetString(method.Name), paramCount));
        return true;
    }
}
