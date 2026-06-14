namespace PeFix.Meta;

public static class RefEvidence
{
    public static RefFinding[] Collect(IReadOnlyList<Inspection> inspections, HostProfile hostProfile)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        ArgumentNullException.ThrowIfNull(hostProfile);

        var dependencies = DependencyIndex.Build(inspections, hostProfile);
        return Collect(Scanner.FindGaps(inspections, dependencies), publishDirProfile: false);
    }

    public static RefFinding[] Collect(ScanReport report)
    {
        return Collect(report.Gaps, publishDirProfile: false);
    }

    public static RefFinding[] Collect(
        ScanReport report,
        HostProfile hostProfile,
        bool publishDirProfile)
    {
        ArgumentNullException.ThrowIfNull(hostProfile);

        RefFinding[] staticFindings = Collect(report.Gaps, publishDirProfile);
        ReflScan reflection = ReflScanner.Scan(report.Results, hostProfile, report.DeclaredAssets);
        return [.. staticFindings, .. MapReflection(reflection, publishDirProfile)];
    }

    private static RefFinding[] Collect(GapSet gaps, bool publishDirProfile)
    {
        List<RefFinding> findings = [];
        findings.AddRange(MapConflicts(gaps.Conflicts));
        findings.AddRange(MapMissingReferences(gaps.MissingReferences));
        findings.AddRange(MapDuplicateProviders(gaps.DuplicateProviders));
        findings.AddRange(MapMemberGaps(gaps.MemberRefGaps));
        findings.AddRange(MapTypeGaps(gaps.TypeRefGaps));
        findings.AddRange(MapFieldGaps(gaps.FieldRefGaps));
        findings.AddRange(MapImplGaps(gaps.ImplGaps));
        findings.AddRange(MapAccessGaps(gaps.AccessGaps, publishDirProfile));
        findings.AddRange(MapNativeGaps(gaps.NativeGaps, publishDirProfile));
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

    private static IEnumerable<RefFinding> MapTypeGaps(TypeRefGap[] gaps)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.TypeGap,
            Confidence: Confidence.Gate,
            ConsumerPath: gap.ConsumerPath,
            ReferenceName: gap.AssemblyName,
            TypeName: gap.TypeName,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: gap.ProviderPath,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapFieldGaps(FieldRefGap[] gaps)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.FieldGap,
            Confidence: Confidence.Gate,
            ConsumerPath: gap.ConsumerPath,
            ReferenceName: gap.AssemblyName,
            TypeName: gap.TypeName,
            MemberName: gap.FieldName,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: gap.ProviderPath,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapImplGaps(ImplGap[] gaps)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.ImplGap,
            Confidence: Confidence.Gate,
            ConsumerPath: gap.ConsumerPath,
            ReferenceName: gap.AssemblyName,
            TypeName: gap.InterfaceName,
            MemberName: gap.MemberName,
            ParameterCount: gap.ParameterCount,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: gap.ProviderPath,
            ProviderPaths: null,
            ImplClass: gap.ClassName));
    }

    private static IEnumerable<RefFinding> MapAccessGaps(
        AccessGap[] gaps,
        bool publishDirProfile)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.AccessGap,
            Confidence: ConfPolicy.For(RefOutcome.AccessGap, publishDirProfile),
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

    private static IEnumerable<RefFinding> MapNativeGaps(
        NativeGap[] gaps,
        bool publishDirProfile)
    {
        return gaps.Select(gap => new RefFinding(
            Tier: RefTier.Native,
            Resolution: RefOutcome.NativeGap,
            Confidence: ConfPolicy.For(RefOutcome.NativeGap, publishDirProfile),
            ConsumerPath: gap.ConsumerPath,
            ReferenceName: gap.ModuleName,
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: gap.RequiredMachine,
            ActualVersion: gap.PresentMachine,
            ProviderPath: gap.PresentPath,
            ProviderPaths: null));
    }

    private static IEnumerable<RefFinding> MapReflection(
        ReflScan reflection,
        bool publishDirProfile)
    {
        return reflection.References.Select(reference => new RefFinding(
            Tier: RefTier.Reflection,
            Resolution: RefOutcome.ReflectionMissing,
            Confidence: ConfPolicy.ForReflection(reference, reflection.HasCustomResolver, publishDirProfile),
            ConsumerPath: reference.ConsumerPath,
            ReferenceName: reference.ReferenceName,
            TypeName: reference.SinkType,
            MemberName: reference.SinkMethod,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: null,
            ProviderPaths: null,
            StaticCtor: reference.StaticCtor));
    }
}
