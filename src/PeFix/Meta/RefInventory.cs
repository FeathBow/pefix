namespace PeFix.Meta;

public static class RefInventory
{
    public static RefEntry[] Collect(
        IReadOnlyList<Inspection> inspections,
        HostProfile hostProfile)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(hostProfile);

        var dependencies = DependencyIndex.Build(inspections, hostProfile);
        List<RefEntry> entries = [];
        foreach (Inspection inspection in inspections)
            AddEntries(entries, inspection, dependencies);

        return [.. entries
            .OrderBy(entry => entry.ReferenceName, StringComparer.Ordinal)
            .ThenBy(entry => entry.RequestedVersion, StringComparer.Ordinal)
            .ThenBy(entry => entry.ConsumerPath, StringComparer.OrdinalIgnoreCase)];
    }

    private static void AddEntries(
        List<RefEntry> entries,
        Inspection inspection,
        DependencyIndex dependencies)
    {
        foreach (AssemblyIdentity reference in inspection.AssemblyReferences ?? [])
            entries.Add(Resolve(inspection.Path, reference, dependencies));
    }

    private static RefEntry Resolve(
        string consumerPath,
        AssemblyIdentity reference,
        DependencyIndex dependencies)
    {
        if (dependencies.ClassifyProvided(reference.Name) != ProvidedKind.None)
            return Create(reference, consumerPath, RefStatus.HostProvided);

        if (!dependencies.TryGetProvider(reference.Name, out Inspection provider))
            return Create(reference, consumerPath, RefStatus.Missing);

        AssemblyIdentity providerIdentity = provider.AssemblyDefinition!.Value;
        RefStatus status = ProviderStatus(reference, providerIdentity);
        return Create(reference, consumerPath, status, provider.Path, providerIdentity.Version);
    }

    private static RefStatus ProviderStatus(
        AssemblyIdentity reference,
        AssemblyIdentity provider)
    {
        return string.Equals(reference.Version, provider.Version, StringComparison.Ordinal)
            ? RefStatus.Present
            : RefStatus.VersionConflict;
    }

    private static RefEntry Create(
        AssemblyIdentity reference,
        string consumerPath,
        RefStatus status,
        string? providerPath = null,
        string? providerVersion = null)
    {
        return new RefEntry(
            reference.Name,
            reference.Version,
            consumerPath,
            status,
            providerPath,
            providerVersion);
    }
}

public enum RefStatus
{
    Present,
    Missing,
    VersionConflict,
    HostProvided
}

public readonly record struct RefEntry(
    string ReferenceName,
    string RequestedVersion,
    string ConsumerPath,
    RefStatus Status,
    string? ProviderPath,
    string? ProviderVersion);
