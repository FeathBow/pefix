namespace PeFix.Cli;

internal static class DirectoryIssueBuilder
{
    public static DirectoryIssue[] Build(
        DirectoryConflict[] conflicts,
        DirectoryMissingReference[] missingReferences,
        DirectoryDuplicateProvider[] duplicateProviders)
    {
        var issues = new List<DirectoryIssue>(conflicts.Length + missingReferences.Length + duplicateProviders.Length);
        AddConflicts(issues, conflicts);
        AddMissingReferences(issues, missingReferences);
        AddDuplicates(issues, duplicateProviders);
        return [.. issues];
    }

    private static void AddConflicts(List<DirectoryIssue> issues, DirectoryConflict[] conflicts)
    {
        foreach (DirectoryConflict conflict in conflicts)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.AsmConflict,
                Subject = conflict.Assembly,
                Summary = $"{conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}.",
                Files = [conflict.ReferencedBy, conflict.ProvidedBy]
            }));
        }
    }

    private static void AddMissingReferences(List<DirectoryIssue> issues, DirectoryMissingReference[] missingReferences)
    {
        foreach (DirectoryMissingReference missingRef in missingReferences)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.MissingRef,
                Subject = missingRef.Assembly,
                Summary = $"{missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found.",
                Files = [missingRef.RequiredBy]
            }));
        }
    }

    private static void AddDuplicates(List<DirectoryIssue> issues, DirectoryDuplicateProvider[] duplicateProviders)
    {
        foreach (DirectoryDuplicateProvider duplicateProvider in duplicateProviders)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.DupProvider,
                Subject = duplicateProvider.Assembly,
                Summary = $"Multiple providers were found: {string.Join(", ", duplicateProvider.Files)}.",
                Files = duplicateProvider.Files
            }));
        }
    }
}
