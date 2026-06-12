namespace PeFix.Cli;

internal sealed class MetricInput
{
    public required ScanFile[] Files { get; init; }
    public required DirectoryIssue[] Issues { get; init; }
    public required DirectoryIssue[] GateIssues { get; init; }
    public required bool HasConflict { get; init; }
    public required int DuplicateCount { get; init; }
}
