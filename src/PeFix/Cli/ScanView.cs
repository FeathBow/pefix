using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanView(
    string Directory,
    ScanStats Stats,
    ScanFile[] Files,
    RefFinding[] Finds,
    DirectoryIssue[] Issues,
    DirectoryIssue[] GateIssues,
    RefEntry[] References)
{
    public bool HasIssues => Issues.Length > 0;

    public bool HasGateIssues => GateIssues.Length > 0;

    public bool HasBlockingFiles => Files.Any(file => file.Status is PeFix.Meta.Status.Unsafe or PeFix.Meta.Status.Corrupt);
}
