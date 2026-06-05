namespace PeFix.Meta;

internal sealed class DependencyIndex
{
    private readonly Dictionary<string, Inspection> _providers;
    private readonly HostProfile _hostProfile;

    private DependencyIndex(Dictionary<string, Inspection> providers, HostProfile hostProfile)
    {
        _providers = providers;
        _hostProfile = hostProfile;
    }

    public static DependencyIndex Build(IReadOnlyList<Inspection> inspections, HostProfile? hostProfile = null)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        hostProfile ??= HostProfile.Default;

        Dictionary<string, Inspection> providers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection inspection in inspections)
        {
            if (inspection.AssemblyDefinition.HasValue)
                providers.TryAdd(inspection.AssemblyDefinition.Value.Name, inspection);
        }

        return new DependencyIndex(providers, hostProfile);
    }

    public bool TryGetProvider(string name, out Inspection inspection)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _providers.TryGetValue(name, out inspection);
    }

    public ProvidedKind ClassifyProvided(string name) => RefFilter.Classify(name, _hostProfile);

    public VersionConflict[] FindConflicts(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        List<VersionConflict> conflicts = [];
        foreach (Inspection inspection in inspections)
        {
            foreach (AssemblyIdentity referenceIdentity in inspection.AssemblyReferences ?? [])
            {
                // Host-provided assemblies (framework, Unity, loader) are unified
                // by the runtime, so version skew against an in-folder copy is not
                // a binding outcome. This mirrors FindMissingReferences, which also
                // skips provided leaves, and avoids false conflicts on real games.
                if (ClassifyProvided(referenceIdentity.Name) != ProvidedKind.None)
                    continue;

                if (!TryGetProvider(referenceIdentity.Name, out Inspection provider))
                    continue;

                AssemblyIdentity providerAssembly = provider.AssemblyDefinition!.Value;
                if (string.Equals(providerAssembly.Version, referenceIdentity.Version, StringComparison.Ordinal))
                    continue;

                conflicts.Add(new VersionConflict(
                    referenceIdentity.Name,
                    referenceIdentity.Version,
                    providerAssembly.Version,
                    inspection.Path,
                    provider.Path));
            }
        }

        return [.. conflicts.DistinctBy(c => (c.AssemblyName, c.ReferencedBy))];
    }

    public MissingReference[] FindMissingReferences(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        List<MissingReference> missing = [];
        foreach (Inspection inspection in inspections)
        {
            foreach (AssemblyIdentity referenceIdentity in inspection.AssemblyReferences ?? [])
            {
                if (TryGetProvider(referenceIdentity.Name, out _) || ClassifyProvided(referenceIdentity.Name) != ProvidedKind.None)
                    continue;

                missing.Add(new MissingReference(
                    referenceIdentity.Name,
                    referenceIdentity.Version,
                    inspection.Path));
            }
        }

        return [.. missing.DistinctBy(item => (item.ReferenceName, item.RequiredBy))];
    }

    public static DuplicateProvider[] FindDuplicateProviders(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        Dictionary<string, List<string>> found = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection item in inspections.Where(item => item.AssemblyDefinition.HasValue))
        {
            string assemblyName = item.AssemblyDefinition!.Value.Name;
            if (!found.TryGetValue(assemblyName, out List<string>? files))
            {
                files = [];
                found[assemblyName] = files;
            }

            files.Add(item.Path);
        }

        return [.. found
            .Where(item => item.Value.Count > 1)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new DuplicateProvider(
                item.Key,
                [.. item.Value.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)]))];
    }
}
