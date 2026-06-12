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
        Dictionary<string, TypeSurface> surfaceByType = new(StringComparer.Ordinal);
        HashSet<string> hiddenTypes = new(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition typeDef = reader.GetTypeDefinition(typeHandle);
            string typeName = ReadTypeName(reader, typeDef.Namespace, typeDef.Name);
            typeNames.Add(typeName);
            surfaceByType[typeName] = ReadTypeSurface(reader, typeDef);
            if (IsHiddenTopLevel(typeDef))
                hiddenTypes.Add(typeName);
        }

        AddForwardedTypes(reader, typeNames);
        return new MemSurface(typeNames, surfaceByType, hiddenTypes);
    }

    private static TypeSurface ReadTypeSurface(MetadataReader reader, TypeDefinition typeDef)
    {
        HashSet<MemberShape> members = ReadMethods(reader, typeDef);
        HashSet<MemberShape> hiddenMembers = HiddenMembers(reader, typeDef, members);
        HashSet<string> fields = ReadFields(reader, typeDef);
        HashSet<string> hiddenFields = HiddenFields(reader, typeDef, fields);
        bool isInterface = (typeDef.Attributes & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface;
        return new TypeSurface
        {
            Members = members,
            Fields = fields,
            HiddenMembers = hiddenMembers.Count > 0 ? hiddenMembers : null,
            HiddenFields = hiddenFields.Count > 0 ? hiddenFields : null,
            Iface = isInterface ? ReadIface(reader, typeDef) : null
        };
    }

    private static bool IsHiddenTopLevel(TypeDefinition typeDef)
    {
        return typeDef.GetDeclaringType().IsNil
            && (typeDef.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic;
    }

    private static HashSet<MemberShape> HiddenMembers(
        MetadataReader reader,
        TypeDefinition typeDef,
        HashSet<MemberShape> members)
    {
        HashSet<MemberShape> visible = [];
        foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            if (!IsVisible(method.Attributes & MethodAttributes.MemberAccessMask))
                continue;

            if (TryDecodeParamCount(method, out int paramCount))
                visible.Add(new MemberShape(reader.GetString(method.Name), paramCount));
        }

        return [.. members.Where(member => !visible.Contains(member))];
    }

    private static HashSet<string> HiddenFields(
        MetadataReader reader,
        TypeDefinition typeDef,
        HashSet<string> fields)
    {
        HashSet<string> visible = new(StringComparer.Ordinal);
        foreach (FieldDefinitionHandle fieldHandle in typeDef.GetFields())
        {
            FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
            if (IsVisible(field.Attributes & FieldAttributes.FieldAccessMask))
                visible.Add(reader.GetString(field.Name));
        }

        HashSet<string> hidden = new(StringComparer.Ordinal);
        hidden.UnionWith(fields.Where(field => !visible.Contains(field)));
        return hidden;
    }

    private static bool IsVisible(MethodAttributes access)
    {
        return access is MethodAttributes.Public
            or MethodAttributes.Family
            or MethodAttributes.FamORAssem;
    }

    private static bool IsVisible(FieldAttributes access)
    {
        return access is FieldAttributes.Public
            or FieldAttributes.Family
            or FieldAttributes.FamORAssem;
    }

    private static IfaceSurface ReadIface(MetadataReader reader, TypeDefinition typeDef)
    {
        HashSet<MemberShape> abstractShapes = [];
        foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            MethodAttributes attrs = method.Attributes;
            if ((attrs & MethodAttributes.Abstract) == 0 || (attrs & MethodAttributes.Static) != 0)
                continue;

            if (TryDecodeParamCount(method, out int paramCount))
                abstractShapes.Add(new MemberShape(reader.GetString(method.Name), paramCount));
        }

        HashSet<string> overrideKeys = new(StringComparer.Ordinal);
        foreach (MethodImplementationHandle handle in typeDef.GetMethodImplementations())
        {
            MethodImplementation impl = reader.GetMethodImplementation(handle);
            if (ImplAnalyzer.TryReadDeclKey(reader, impl.MethodDeclaration, out _, out string dimKey))
                overrideKeys.Add(dimKey);
        }

        return new IfaceSurface(abstractShapes, overrideKeys);
    }

    private static void AddForwardedTypes(MetadataReader reader, HashSet<string> typeNames)
    {
        foreach (ExportedTypeHandle handle in reader.ExportedTypes)
        {
            ExportedType type = reader.GetExportedType(handle);
            if (!type.Attributes.HasFlag(ForwarderAttr))
                continue;

            typeNames.Add(ReadTypeName(reader, type.Namespace, type.Name));
        }
    }

    internal static HashSet<MemberShape> ReadMethods(
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
            ReadTypeName(reader, typeRef.Namespace, typeRef.Name),
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
        if (!dependencies.TryGetProviderView(methodRef.AssemblyName, out Inspection provider, out PeView providerView))
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
        if (!dependencies.TryGetProviderView(typeRef.AssemblyName, out Inspection provider, out PeView providerView))
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

    internal static bool TryDecodeParamCount(MemberReference member, out int paramCount)
    {
        paramCount = 0;
        if (member.GetKind() != MemberReferenceKind.Method)
            return false;

        return TryDecodeParamCount(
            () => member.DecodeMethodSignature(new ParamCounter(), null),
            out paramCount);
    }

    internal static bool TryDecodeParamCount(MethodDefinition method, out int paramCount)
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

    internal static string ReadTypeName(MetadataReader reader, StringHandle ns, StringHandle name)
    {
        string nsValue = reader.GetString(ns);
        string typeName = reader.GetString(name);
        return nsValue.Length == 0 ? typeName : $"{nsValue}.{typeName}";
    }

}
