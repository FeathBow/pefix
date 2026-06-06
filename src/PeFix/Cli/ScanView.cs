using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanView(
    string Directory,
    ScanStats Stats,
    ScanFile[] Files,
    DirectoryConflict[] Conflicts,
    DirectoryMissingReference[] MissingReferences,
    DirectoryDuplicateProvider[] DuplicateProviders,
    DirectoryIssue[] Issues,
    int GateIssueCount,
    RefEntry[] References)
{
    public bool HasIssues => Issues.Length > 0;

    public bool HasGateIssues => GateIssueCount > 0;

    public bool HasBlockingFiles => Files.Any(file => file.Status is PeFix.Meta.Status.Unsafe or PeFix.Meta.Status.Corrupt);
}
