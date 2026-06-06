namespace PeFix.Meta;

public static class RefEvidence
{
    public static RefFinding[] Collect(IReadOnlyList<Inspection> inspections, HostProfile hostProfile)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(hostProfile);

        var dependencies = DependencyIndex.Build(inspections, hostProfile);
        return Collect(
            dependencies.FindConflicts(inspections),
            dependencies.FindMissingReferences(inspections),
            DependencyIndex.FindDuplicateProviders(inspections),
            MemberSurfaceAnalyzer.FindMethodGaps(inspections, dependencies));
    }

    public static RefFinding[] Collect(ScanReport report)
    {
        return Collect(
            report.Conflicts,
            report.MissingReferences,
            report.DuplicateProviders,
            report.MemberRefGaps);
    }

    public static RefFinding[] Collect(
        ScanReport report,
        HostProfile hostProfile,
        bool publishDirProfile)
    {
        ArgumentNullException.ThrowIfNull(hostProfile);

        RefFinding[] staticFindings = Collect(report);
        ReflScan reflection = ReflScanner.Scan(report.Results, hostProfile);
        return [.. staticFindings, .. MapReflection(reflection, publishDirProfile)];
    }

    private static RefFinding[] Collect(
        VersionConflict[] conflicts,
        MissingReference[] missingReferences,
        DuplicateProvider[] duplicateProviders,
        MemberRefGap[] memberGaps)
    {
        List<RefFinding> findings = [];
        findings.AddRange(MapConflicts(conflicts));
        findings.AddRange(MapMissingReferences(missingReferences));
        findings.AddRange(MapDuplicateProviders(duplicateProviders));
        findings.AddRange(MapMemberGaps(memberGaps));
        return [.. findings];
    }

    private static IEnumerable<RefFinding> MapConflicts(VersionConflict[] conflicts)
    {
        return conflicts.Select(conflict => new RefFinding(
            Tier: RefTier.AssemblyRef,
            Resolution: RefOutcome.VersionConflict,
            Confidence: Confidence.Gate,
            ConsumerPath: conflict.ReferencedBy,
            ReferenceName: conflict.AssemblyName,
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: conflict.Expected,
            ActualVersion: conflict.Actual,
            ProviderPath: conflict.ProvidedBy,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapMissingReferences(MissingReference[] missingReferences)
    {
        return missingReferences.Select(missing => new RefFinding(
            Tier: RefTier.AssemblyRef,
            Resolution: RefOutcome.Missing,
            Confidence: Confidence.Gate,
            ConsumerPath: missing.RequiredBy,
            ReferenceName: missing.ReferenceName,
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: missing.RequiredVersion,
            ActualVersion: null,
            ProviderPath: null,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapDuplicateProviders(DuplicateProvider[] duplicateProviders)
    {
        return duplicateProviders.Select(duplicate => new RefFinding(
            Tier: RefTier.Provider,
            Resolution: RefOutcome.DuplicateProvider,
            Confidence: Confidence.Gate,
            ConsumerPath: string.Empty,
            ReferenceName: duplicate.AssemblyName,
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: null,
            duplicate.Files));
    }

    private static IEnumerable<RefFinding> MapMemberGaps(MemberRefGap[] gaps)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.MemberGap,
            Confidence: Confidence.Gate,
            ConsumerPath: gap.ConsumerPath,
            ReferenceName: gap.AssemblyName,
            TypeName: gap.TypeName,
            MemberName: gap.MemberName,
            ParameterCount: gap.ParameterCount,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: gap.ProviderPath,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapReflection(
        ReflScan reflection,
        bool publishDirProfile)
    {
        return reflection.References.Select(reference => new RefFinding(
            Tier: RefTier.Reflection,
            Resolution: RefOutcome.ReflectionMissing,
            Confidence: ReflectionConfidence(reference, reflection.HasCustomResolver, publishDirProfile),
            ConsumerPath: reference.ConsumerPath,
            ReferenceName: reference.ReferenceName,
            TypeName: reference.SinkType,
            MemberName: reference.SinkMethod,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: null,
            ProviderPaths: null));
    }

    private static Confidence ReflectionConfidence(
        ReflRef reference,
        bool hasCustomResolver,
        bool publishDirProfile)
    {
        return publishDirProfile && !hasCustomResolver && !reference.AdvisoryOnly
            ? Confidence.Gate
            : Confidence.Advisory;
    }
}
