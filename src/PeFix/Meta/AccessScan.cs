using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static class AccessScan
{
    private const string AttrNamespace = "System.Runtime.CompilerServices";
    private const string IvtAttr = "InternalsVisibleToAttribute";
    private const string SkipAttr = "IgnoresAccessChecksToAttribute";

    public static AccessGap[] FindAccessGaps(
        IReadOnlyList<Inspection> inspections,
        DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(dependencies);

        List<AccessGap> gaps = [];
        foreach (Inspection consumer in inspections)
        {
            if (consumer.View is not { } view)
                continue;

            if (consumer.AssemblyDefinition is not { } consumerIdentity)
                continue;

            foreach (MethodRefUse methodRef in view.MethodRefs)
                AddMethodGap(gaps, methodRef, view, consumerIdentity.Name, dependencies, consumer);

            foreach (FieldRefUse fieldRef in view.FieldRefs)
                AddFieldGap(gaps, fieldRef, view, consumerIdentity.Name, dependencies, consumer);
        }

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ProviderPath,
            item.AssemblyName,
            item.TypeName,
            item.MemberName,
            item.ParameterCount))];
    }

    internal static AccessInfo ReadAccessInfo(MetadataReader reader)
    {
        if (!reader.IsAssembly)
            return AccessInfo.Empty;

        HashSet<string> ivtNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> skipNames = new(StringComparer.OrdinalIgnoreCase);
        AssemblyDefinition assemblyDef = reader.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in assemblyDef.GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (AttrReader.IsMatch(reader, attr, AttrNamespace, IvtAttr))
                AddSimpleName(ivtNames, ReadFirstString(attr));
            else if (AttrReader.IsMatch(reader, attr, AttrNamespace, SkipAttr))
                AddSimpleName(skipNames, ReadFirstString(attr));
        }

        return new AccessInfo(ivtNames, skipNames);
    }

    private static string? ReadFirstString(CustomAttribute attr)
    {
        try
        {
            return AttrReader.ReadFixedString(attr, 0);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void AddSimpleName(HashSet<string> names, string? declared)
    {
        if (declared is null)
            return;

        int comma = declared.IndexOf(',', StringComparison.Ordinal);
        string simpleName = (comma < 0 ? declared : declared[..comma]).Trim();
        if (simpleName.Length > 0)
            names.Add(simpleName);
    }

    private static void AddMethodGap(
        List<AccessGap> gaps,
        MethodRefUse methodRef,
        PeView view,
        string consumerName,
        DependencyIndex dependencies,
        Inspection consumer)
    {
        if (!TryGetProviderSurface(methodRef.AssemblyName, view, consumerName, dependencies, out Inspection provider, out MemSurface surface))
            return;

        if (surface.IsHiddenType(methodRef.TypeName))
        {
            gaps.Add(TypeGap(methodRef.AssemblyName, methodRef.TypeName, consumer, provider));
            return;
        }

        if (!surface.TryGetSurface(methodRef.TypeName, out TypeSurface typeSurface))
            return;

        if (!typeSurface.IsHiddenMember(new MemberShape(methodRef.MemberName, methodRef.ParameterCount)))
            return;

        gaps.Add(new AccessGap(
            methodRef.AssemblyName,
            methodRef.TypeName,
            methodRef.MemberName,
            methodRef.ParameterCount,
            consumer.Path,
            provider.Path));
    }

    private static void AddFieldGap(
        List<AccessGap> gaps,
        FieldRefUse fieldRef,
        PeView view,
        string consumerName,
        DependencyIndex dependencies,
        Inspection consumer)
    {
        if (!TryGetProviderSurface(fieldRef.AssemblyName, view, consumerName, dependencies, out Inspection provider, out MemSurface surface))
            return;

        if (surface.IsHiddenType(fieldRef.TypeName))
        {
            gaps.Add(TypeGap(fieldRef.AssemblyName, fieldRef.TypeName, consumer, provider));
            return;
        }

        if (!surface.TryGetSurface(fieldRef.TypeName, out TypeSurface typeSurface))
            return;

        if (!typeSurface.IsHiddenField(fieldRef.FieldName))
            return;

        gaps.Add(new AccessGap(
            fieldRef.AssemblyName,
            fieldRef.TypeName,
            fieldRef.FieldName,
            null,
            consumer.Path,
            provider.Path));
    }

    private static AccessGap TypeGap(
        string assemblyName,
        string typeName,
        Inspection consumer,
        Inspection provider)
    {
        return new AccessGap(assemblyName, typeName, null, null, consumer.Path, provider.Path);
    }

    private static bool TryGetProviderSurface(
        string assemblyName,
        PeView view,
        string consumerName,
        DependencyIndex dependencies,
        out Inspection provider,
        out MemSurface surface)
    {
        provider = default!;
        surface = null!;
        if (string.Equals(assemblyName, consumerName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (view.AccessInfo.SkipNames.Contains(assemblyName))
            return false;

        if (!dependencies.TryGetProviderView(assemblyName, out provider, out PeView providerView))
            return false;

        if (providerView.AccessInfo.IvtNames.Contains(consumerName))
            return false;

        surface = providerView.MemSurface;
        return true;
    }
}
