using PeFix.Meta;

namespace PeFix.Cli;

internal static class DirectoryIssueBuilder
{
    public static DirectoryIssue[] Build(RefFinding[] findings, PathRelativizer rel)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(rel);

        var issues = new List<DirectoryIssue>(findings.Length);
        AddConflicts(issues, findings, rel);
        AddMissingReferences(issues, findings, rel);
        AddReflectionMissing(issues, findings, rel);
        AddDuplicates(issues, findings, rel);
        AddMissingTypes(issues, findings, rel);
        AddMissingMembers(issues, findings, rel);
        return [.. issues];
    }

    private static void AddConflicts(List<DirectoryIssue> issues, RefFinding[] findings, PathRelativizer rel)
    {
        foreach (RefFinding conflict in Ordered(findings, RefOutcome.VersionConflict))
        {
            string referencedBy = rel.RelativePath(conflict.ConsumerPath);
            string providedBy = rel.RelativePath(Required(conflict.ProviderPath, nameof(conflict.ProviderPath)));
            issues.Add(RepairGuide.ForIssue(
                IssueCode.AsmConflict,
                conflict.ReferenceName,
                $"{referencedBy} expects v{Required(conflict.ExpectedVersion, nameof(conflict.ExpectedVersion))}, but v{Required(conflict.ActualVersion, nameof(conflict.ActualVersion))} is provided by {providedBy}.",
                [referencedBy, providedBy]));
        }
    }

    private static void AddMissingReferences(List<DirectoryIssue> issues, RefFinding[] findings, PathRelativizer rel)
    {
        foreach (RefFinding missingRef in Ordered(findings, RefOutcome.Missing))
        {
            string requiredBy = rel.RelativePath(missingRef.ConsumerPath);
            issues.Add(RepairGuide.ForIssue(
                IssueCode.MissingRef,
                missingRef.ReferenceName,
                $"{requiredBy} expects v{Required(missingRef.ExpectedVersion, nameof(missingRef.ExpectedVersion))}, but no provider was found.",
                [requiredBy]));
        }
    }

    private static void AddDuplicates(List<DirectoryIssue> issues, RefFinding[] findings, PathRelativizer rel)
    {
        foreach (RefFinding duplicateProvider in Ordered(findings, RefOutcome.DuplicateProvider))
        {
            string[] files = rel.RelativePaths(Required(duplicateProvider.ProviderPaths, nameof(duplicateProvider.ProviderPaths)));
            issues.Add(RepairGuide.ForIssue(
                IssueCode.DupProvider,
                duplicateProvider.ReferenceName,
                $"Multiple providers were found: {string.Join(", ", files)}.",
                files));
        }
    }

    private static void AddReflectionMissing(
        List<DirectoryIssue> issues,
        RefFinding[] findings,
        PathRelativizer rel)
    {
        foreach (RefFinding missing in Ordered(findings, RefOutcome.ReflectionMissing))
            issues.Add(CreateReflectionMissingIssue(missing, rel));
    }

    private static DirectoryIssue CreateReflectionMissingIssue(
        RefFinding missing,
        PathRelativizer rel)
    {
        string consumer = rel.RelativePath(missing.ConsumerPath);
        string sink = $"{Required(missing.TypeName, nameof(missing.TypeName))}.{Required(missing.MemberName, nameof(missing.MemberName))}";
        return RepairGuide.ForIssue(
            IssueCode.ReflectionMissing,
            missing.ReferenceName,
            $"{consumer} has a literal {sink} load for {missing.ReferenceName}, but no provider was found.",
            [consumer]);
    }

    private static void AddMissingMembers(
        List<DirectoryIssue> issues,
        RefFinding[] findings,
        PathRelativizer rel)
    {
        foreach (RefFinding gap in Ordered(findings, RefOutcome.MemberGap)
            .ThenBy(item => item.MemberName, StringComparer.Ordinal))
            issues.Add(CreateMissingMemberIssue(gap, rel));
    }

    private static void AddMissingTypes(
        List<DirectoryIssue> issues,
        RefFinding[] findings,
        PathRelativizer rel)
    {
        foreach (RefFinding gap in Ordered(findings, RefOutcome.TypeGap)
            .ThenBy(item => item.TypeName, StringComparer.Ordinal))
            issues.Add(CreateMissingTypeIssue(gap, rel));
    }

    private static DirectoryIssue CreateMissingTypeIssue(RefFinding gap, PathRelativizer rel)
    {
        string requiredBy = rel.RelativePath(gap.ConsumerPath);
        string providedBy = rel.RelativePath(Required(gap.ProviderPath, nameof(gap.ProviderPath)));
        string typeName = Required(gap.TypeName, nameof(gap.TypeName));
        return RepairGuide.ForIssue(
            IssueCode.MissingType,
            gap.ReferenceName,
            $"Type '{typeName}' not found in {providedBy}; consumed by {requiredBy}.",
            [requiredBy, providedBy],
            IssueEvidence.ForMissingType(typeName, providedBy));
    }

    private static DirectoryIssue CreateMissingMemberIssue(RefFinding gap, PathRelativizer rel)
    {
        string requiredBy = rel.RelativePath(gap.ConsumerPath);
        string providedBy = rel.RelativePath(Required(gap.ProviderPath, nameof(gap.ProviderPath)));
        return RepairGuide.ForIssue(
            IssueCode.MissingMember,
            gap.ReferenceName,
            $"{requiredBy} references {Required(gap.TypeName, nameof(gap.TypeName))}.{Required(gap.MemberName, nameof(gap.MemberName))}/{Required(gap.ParameterCount, nameof(gap.ParameterCount))} on {gap.ReferenceName}, but {providedBy} does not expose a matching member at tier {MemberSurfaceAnalyzer.ConservativeMatchingTier}.",
            [requiredBy, providedBy],
            IssueEvidence.ForMissingMember(
                Required(gap.TypeName, nameof(gap.TypeName)),
                Required(gap.MemberName, nameof(gap.MemberName)),
                Required(gap.ParameterCount, nameof(gap.ParameterCount)),
                MemberSurfaceAnalyzer.ConservativeMatchingTier,
                providedBy));
    }

    private static IOrderedEnumerable<RefFinding> Ordered(
        RefFinding[] findings,
        RefOutcome resolution)
    {
        return findings
            .Where(item => item.Resolution == resolution)
            .OrderBy(item => item.ReferenceName, StringComparer.Ordinal);
    }

    private static string Required(string? value, string fieldName)
    {
        return value ?? throw new InvalidOperationException($"Reference finding is missing {fieldName}.");
    }

    private static string[] Required(string[]? value, string fieldName)
    {
        return value ?? throw new InvalidOperationException($"Reference finding is missing {fieldName}.");
    }

    private static int Required(int? value, string fieldName)
    {
        return value ?? throw new InvalidOperationException($"Reference finding is missing {fieldName}.");
    }
}
