namespace PeFix.Cli;

internal sealed class ScanMetrics
{
    public required int FileCount { get; init; }
    public required int DuplicateCount { get; init; }
    public required ScanStats Stats { get; init; }
    public required Dictionary<string, int> ByCategory { get; init; }
    public required Dictionary<string, int> ByAction { get; init; }
    public required Dictionary<string, int> ByIssue { get; init; }
    public required int GateIssueCount { get; init; }
    public required string[] GateIssueCodes { get; init; }
    public required int BlockingFileCount { get; init; }
    public required string[] BlockingFileReasons { get; init; }
}
