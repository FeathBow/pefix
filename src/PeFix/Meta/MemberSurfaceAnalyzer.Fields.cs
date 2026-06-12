using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static partial class MemberSurfaceAnalyzer
{
    public static FieldRefGap[] FindFieldGaps(
        IReadOnlyList<Inspection> inspections,
        DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<FieldRefGap> gaps = [];
        foreach (Inspection consumer in inspections)
            AddFieldGaps(gaps, consumer, dependencies);

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ProviderPath,
            item.AssemblyName,
            item.TypeName,
            item.FieldName,
            item.MatchingTier))];
    }

    internal static FieldRefUse[] ReadFieldRefs(MetadataReader reader)
    {
        List<FieldRefUse> fieldRefs = [];
        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            if (TryReadFieldRef(reader, handle, out FieldRefUse fieldRef))
                fieldRefs.Add(fieldRef);
        }

        return [.. fieldRefs];
    }

    private static IEnumerable<TypeRefUse> TypeRefs(PeView view)
    {
        foreach (MethodRefUse methodRef in view.MethodRefs)
            yield return new TypeRefUse(methodRef.AssemblyName, methodRef.TypeName);

        foreach (FieldRefUse fieldRef in view.FieldRefs)
            yield return new TypeRefUse(fieldRef.AssemblyName, fieldRef.TypeName);
    }

    private static void AddFieldGaps(
        List<FieldRefGap> gaps,
        Inspection consumer,
        DependencyIndex deps)
    {
        if (consumer.View is not { } view)
            return;

        foreach (FieldRefUse fieldRef in view.FieldRefs)
        {
            if (TryBuildFieldRefGap(fieldRef, deps, consumer, out FieldRefGap gap))
                gaps.Add(gap);
        }
    }

    private static bool TryReadFieldRef(
        MetadataReader reader,
        MemberReferenceHandle handle,
        out FieldRefUse fieldRef)
    {
        fieldRef = default;
        MemberReference member = reader.GetMemberReference(handle);
        if (member.GetKind() != MemberReferenceKind.Field)
            return false;

        if (member.Parent.Kind != HandleKind.TypeReference)
            return false;

        TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
            return false;

        AssemblyReference asm = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
        fieldRef = new FieldRefUse(
            reader.GetString(asm.Name),
            TypeName(reader, typeRef.Namespace, typeRef.Name),
            reader.GetString(member.Name));
        return true;
    }

    private static bool TryBuildFieldRefGap(
        FieldRefUse fieldRef,
        DependencyIndex deps,
        Inspection consumer,
        out FieldRefGap gap)
    {
        gap = default;
        if (deps.ClassifyProvided(fieldRef.AssemblyName) != ProvidedKind.None)
            return false;

        if (!deps.TryGetProvider(fieldRef.AssemblyName, out Inspection provider))
            return false;

        if (provider.View is not { } providerView)
            return false;

        if (!providerView.MemSurface.ContainsType(fieldRef.TypeName))
            return false;

        if (!providerView.MemSurface.TryGetFields(fieldRef.TypeName, out HashSet<string> fields))
            return false;

        if (fields.Contains(fieldRef.FieldName))
            return false;

        gap = new FieldRefGap(
            fieldRef.AssemblyName,
            fieldRef.TypeName,
            fieldRef.FieldName,
            FieldTier,
            consumer.Path,
            provider.Path);
        return true;
    }
}
