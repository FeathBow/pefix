using PeFix.Meta;

namespace PeFix.Cli;

internal static class DirectoryIssueBuilder
{
    public static DirectoryIssue[] Build(IssueSources input)
    {
        var issues = new List<DirectoryIssue>(
            input.Conflicts.Length +
            input.MissingReferences.Length +
            input.DuplicateProviders.Length +
            input.MemberRefGaps.Length);
        AddConflicts(issues, input.Conflicts);
        AddMissingReferences(issues, input.MissingReferences);
        AddDuplicates(issues, input.DuplicateProviders);
        AddMissingMembers(issues, input.MemberRefGaps, input.Rel);
        return [.. issues];
    }

    private static void AddConflicts(List<DirectoryIssue> issues, DirectoryConflict[] conflicts)
    {
        foreach (DirectoryConflict conflict in conflicts)
        {
            issues.Add(RepairGuide.ForIssue(
                IssueCode.AsmConflict,
                conflict.Assembly,
                $"{conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}.",
                [conflict.ReferencedBy, conflict.ProvidedBy]));
        }
    }

    private static void AddMissingReferences(List<DirectoryIssue> issues, DirectoryMissingReference[] missingReferences)
    {
        foreach (DirectoryMissingReference missingRef in missingReferences)
        {
            issues.Add(RepairGuide.ForIssue(
                IssueCode.MissingRef,
                missingRef.Assembly,
                $"{missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found.",
                [missingRef.RequiredBy]));
        }
    }

    private static void AddDuplicates(List<DirectoryIssue> issues, DirectoryDuplicateProvider[] duplicateProviders)
    {
        foreach (DirectoryDuplicateProvider duplicateProvider in duplicateProviders)
        {
            issues.Add(RepairGuide.ForIssue(
                IssueCode.DupProvider,
                duplicateProvider.Assembly,
                $"Multiple providers were found: {string.Join(", ", duplicateProvider.Files)}.",
                duplicateProvider.Files));
        }
    }

    private static void AddMissingMembers(
        List<DirectoryIssue> issues,
        MemberRefGap[] memberRefGaps,
        PathRelativizer rel)
    {
        foreach (MemberRefGap gap in memberRefGaps
            .OrderBy(item => item.AssemblyName, StringComparer.Ordinal)
            .ThenBy(item => item.MemberName, StringComparer.Ordinal))
            issues.Add(CreateMissingMemberIssue(gap, rel));
    }

    private static DirectoryIssue CreateMissingMemberIssue(MemberRefGap gap, PathRelativizer rel)
    {
        string requiredBy = rel.RelativePath(gap.ConsumerPath);
        string providedBy = rel.RelativePath(gap.ProviderPath);
        return RepairGuide.ForIssue(
            IssueCode.MissingMember,
            gap.AssemblyName,
            $"{requiredBy} references {gap.TypeName}.{gap.MemberName}/{gap.ParameterCount} on {gap.AssemblyName}, but {providedBy} does not expose a matching member at tier {gap.MatchingTier}.",
            [requiredBy, providedBy],
            IssueEvidence.ForMissingMember(
                gap.TypeName,
                gap.MemberName,
                gap.ParameterCount,
                gap.MatchingTier,
                providedBy));
    }
}

internal sealed class IssueSources
{
    public required DirectoryConflict[] Conflicts { get; init; }
    public required DirectoryMissingReference[] MissingReferences { get; init; }
    public required DirectoryDuplicateProvider[] DuplicateProviders { get; init; }
    public required MemberRefGap[] MemberRefGaps { get; init; }
    public required PathRelativizer Rel { get; init; }
}
