namespace PeFix.Cli;

internal sealed record ScanView(
    string Directory,
    ScanStats Stats,
    ScanFile[] Files,
    DirectoryConflict[] Conflicts,
    DirectoryMissingReference[] MissingReferences,
    DirectoryDuplicateProvider[] DuplicateProviders,
    DirectoryIssue[] Issues,
    ScanJsonMeta? Json = null)
{
    public bool HasIssues => Issues.Length > 0;
}
