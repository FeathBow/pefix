using PeFix.Meta;

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

internal sealed class MetricInput
{
    public required ScanFile[] Files { get; init; }
    public required DirectoryIssue[] Issues { get; init; }
    public required DirectoryIssue[] GateIssues { get; init; }
    public required bool HasConflict { get; init; }
    public required int DuplicateCount { get; init; }
}

internal sealed class ScanInput
{
    public required Inspection[] Results { get; init; }
    public required ScanProfile? Profile { get; init; }
    public required BepInExProviderIndex BepInExProviderIndex { get; init; }
    public required BepInExExplainResult BepInExExplain { get; init; }
    public required IReadOnlyDictionary<string, LoaderTarget> LoaderByPath { get; init; }
    public required ScanMetrics Metrics { get; init; }
    public required RefEntry[]? References { get; init; }
}

internal sealed class ScanBuildCtx
{
    public required ScanReport Report { get; init; }
    public required ScanProfile? Profile { get; init; }
    public required PathRelativizer Rel { get; init; }
    public required BepInExProviderIndex BepInExProviderIndex { get; init; }
    public required IReadOnlyDictionary<string, LoaderTarget> LoaderByPath { get; init; }
}

internal sealed class IssueBuild
{
    public required DirectoryIssue[] Issues { get; init; }
    public required DirectoryIssue[] GateIssues { get; init; }
}
