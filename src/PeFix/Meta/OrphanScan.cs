namespace PeFix.Meta;

internal static class OrphanScan
{
    private const string ResourceSuffix = ".resources";

    public static string[] FindOrphans(IReadOnlyList<Inspection> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        HashSet<string> referenced = CollectReferencedNames(results);
        return [.. results
            .Where(item => IsOrphan(item, referenced))
            .Select(item => item.Path)
            .OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static HashSet<string> CollectReferencedNames(IReadOnlyList<Inspection> results)
    {
        HashSet<string> referenced = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection item in results)
        {
            foreach (AssemblyIdentity reference in item.AssemblyReferences ?? [])
                referenced.Add(reference.Name);

            if (item.View is not { } view)
                continue;

            foreach (ReflRef reflection in view.Reflection.References)
                referenced.Add(reflection.ReferenceName);
        }

        return referenced;
    }

    private static bool IsOrphan(Inspection item, HashSet<string> referenced)
    {
        if (item.AssemblyDefinition is not { } identity)
            return false;

        if (item.HasEntryPoint)
            return false;

        if (item.BepInEx is { Plugins.Length: > 0 })
            return false;

        if (identity.Name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            return false;

        return !referenced.Contains(identity.Name);
    }
}
