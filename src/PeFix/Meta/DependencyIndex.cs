namespace PeFix.Meta;

internal sealed class DependencyIndex
{
    private readonly Dictionary<string, Inspection> _providers;
    private readonly HostProfile _hostProfile;
    private readonly IReadOnlySet<string>? _declaredAssets;

    private DependencyIndex(
        Dictionary<string, Inspection> providers,
        HostProfile hostProfile,
        IReadOnlySet<string>? declaredAssets)
    {
        _providers = providers;
        _hostProfile = hostProfile;
        _declaredAssets = declaredAssets;
    }

    public static DependencyIndex Build(
        IReadOnlyList<Inspection> inspections,
        HostProfile? hostProfile = null,
        IReadOnlySet<string>? declaredAssets = null)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        hostProfile ??= HostProfile.Default;

        Dictionary<string, Inspection> providers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection inspection in inspections)
        {
            if (inspection.AssemblyDefinition.HasValue)
                providers.TryAdd(inspection.AssemblyDefinition.Value.Name, inspection);
        }

        return new DependencyIndex(providers, hostProfile, declaredAssets);
    }

    public bool TryGetProvider(string name, out Inspection inspection)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _providers.TryGetValue(name, out inspection);
    }

    public bool TryGetProviderView(string name, out Inspection provider, out PeView view)
    {
        view = null!;
        provider = default;
        if (ClassifyProvided(name) != ProvidedKind.None)
            return false;

        if (!TryGetProvider(name, out provider))
            return false;

        if (provider.View is not { } providerView)
            return false;

        view = providerView;
        return true;
    }

    public ProvidedKind ClassifyProvided(string name)
    {
        ProvidedKind byName = RefFilter.Classify(name, _hostProfile);
        if (byName != ProvidedKind.None)
            return byName;

        // A reference absent from the deps.json runtime-asset set is resolved from a
        // shared framework, i.e. externally provided.
        if (_declaredAssets is not null && !_declaredAssets.Contains(name))
            return ProvidedKind.Framework;

        return ProvidedKind.None;
    }

    // A v0.0.0.0 reference is version-neutral: facades and type-forward shims emit it and
    // bind to whatever version ships, so it is never a real conflict.
    public static bool IsVersionNeutral(string version) =>
        string.Equals(version, "0.0.0.0", StringComparison.Ordinal);

    // runtimes/<rid>/ holds RID-specific NuGet assets; the host loads only the matching
    // RID, so root-vs-runtimes and cross-RID copies are not duplicate providers.
    private static bool IsRidSpecific(string path) =>
        path.Replace('\\', '/').Contains("/runtimes/", StringComparison.OrdinalIgnoreCase);

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

                if (IsVersionNeutral(referenceIdentity.Version))
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
        foreach (Inspection item in inspections.Where(item => item.AssemblyDefinition.HasValue && !IsRidSpecific(item.Path)))
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
