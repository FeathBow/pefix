namespace PeFix.Meta;

internal sealed class DepIndex
{
    private readonly Dictionary<string, Inspection> _providers;

    private DepIndex(Dictionary<string, Inspection> providers)
    {
        _providers = providers;
    }

    public static DepIndex Build(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        Dictionary<string, Inspection> providers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection inspection in inspections)
        {
            if (inspection.AssemblyDef.HasValue)
                providers.TryAdd(inspection.AssemblyDef.Value.Name, inspection);
        }

        return new DepIndex(providers);
    }

    public bool TryGetProvider(string name, out Inspection inspection)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _providers.TryGetValue(name, out inspection);
    }

    public static ProvidedKind ClassifyProvided(string name) => RefFilter.Classify(name);

    public VerConflict[] FindConflicts(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        List<VerConflict> conflicts = [];
        foreach (Inspection inspection in inspections)
        {
            foreach (AsmRef asmRef in inspection.AssemblyRefs ?? [])
            {
                if (!TryGetProvider(asmRef.Name, out Inspection provider))
                    continue;

                AsmRef providerDef = provider.AssemblyDef!.Value;
                if (string.Equals(providerDef.Version, asmRef.Version, StringComparison.Ordinal))
                    continue;

                conflicts.Add(new VerConflict(
                    asmRef.Name,
                    asmRef.Version,
                    providerDef.Version,
                    inspection.Path,
                    provider.Path));
            }
        }

        return [.. conflicts.DistinctBy(c => (c.AssemblyName, c.ReferencedBy))];
    }

    public MissingRef[] FindMissing(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        List<MissingRef> missing = [];
        foreach (Inspection inspection in inspections)
        {
            foreach (AsmRef asmRef in inspection.AssemblyRefs ?? [])
            {
                if (TryGetProvider(asmRef.Name, out _) || ClassifyProvided(asmRef.Name) != ProvidedKind.None)
                    continue;

                missing.Add(new MissingRef(
                    asmRef.Name,
                    asmRef.Version,
                    inspection.Path));
            }
        }

        return [.. missing.DistinctBy(item => (item.RefName, item.NeedBy))];
    }

    public static DupProvider[] FindDuplicates(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        Dictionary<string, List<string>> found = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection item in inspections.Where(item => item.AssemblyDef.HasValue))
        {
            string asmName = item.AssemblyDef!.Value.Name;
            if (!found.TryGetValue(asmName, out List<string>? files))
            {
                files = [];
                found[asmName] = files;
            }

            files.Add(item.Path);
        }

        return [.. found
            .Where(item => item.Value.Count > 1)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new DupProvider(
                item.Key,
                [.. item.Value.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)]))];
    }
}
