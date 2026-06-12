using PeFix.Meta;

namespace PeFix.Cli;

internal static class MetricBuild
{
    public static ScanMetrics Build(MetricInput input)
    {
        FileMetrics files = CountFiles(input.Files);
        IssueMetrics issues = CountIssues(input.Issues);
        return new ScanMetrics
        {
            FileCount = input.Files.Length,
            DuplicateCount = input.DuplicateCount,
            Stats = new ScanStats(files.Counts, files.HasFixable, input.HasConflict),
            ByCategory = files.ByCategory,
            ByAction = files.ByAction,
            ByIssue = issues.ByIssue,
            GateIssueCount = input.GateIssues.Length,
            GateIssueCodes = GateIssueCodes(input.GateIssues),
            BlockingFileCount = files.BlockingFileCount,
            BlockingFileReasons = [.. files.BlockingReasons.OrderBy(reason => reason, StringComparer.Ordinal)]
        };
    }

    private static FileMetrics CountFiles(ScanFile[] files)
    {
        ScanCounts counts = new(0, 0, 0, 0, 0);
        bool hasFixable = false;
        int blockingFileCount = 0;
        var byCategory = new Dictionary<string, int>(StringComparer.Ordinal);
        var byAction = new Dictionary<string, int>(StringComparer.Ordinal);
        var blockingReasons = new HashSet<string>(StringComparer.Ordinal);

        foreach (ScanFile file in files)
        {
            counts = CountStatus(counts, file.Status);
            AddCount(byCategory, file.Category);
            AddCount(byAction, file.ActionText);
            if (file.CanPatch)
                hasFixable = true;

            if (file.Status is Status.Unsafe or Status.Corrupt)
            {
                blockingFileCount++;
                blockingReasons.Add(file.ReasonCode);
            }
        }

        return new FileMetrics(
            counts,
            hasFixable,
            blockingFileCount,
            blockingReasons,
            byCategory,
            byAction);
    }

    private static IssueMetrics CountIssues(DirectoryIssue[] issues)
    {
        var byIssue = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (DirectoryIssue issue in issues)
            AddCount(byIssue, issue.Code);

        return new IssueMetrics(byIssue);
    }

    private static string[] GateIssueCodes(DirectoryIssue[] gateIssues)
    {
        return [.. gateIssues
            .Select(issue => issue.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)];
    }

    private static ScanCounts CountStatus(ScanCounts counts, Status status)
    {
        return status switch
        {
            Status.Compatible => counts with { Compatible = counts.Compatible + 1 },
            Status.Fixable => counts with { Fixable = counts.Fixable + 1 },
            Status.Cautioned => counts with { Cautioned = counts.Cautioned + 1 },
            Status.Unsafe => counts with { Unsafe = counts.Unsafe + 1 },
            Status.Corrupt => counts with { Corrupt = counts.Corrupt + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported inspection status.")
        };
    }

    private static void AddCount(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    private sealed record FileMetrics(
        ScanCounts Counts,
        bool HasFixable,
        int BlockingFileCount,
        HashSet<string> BlockingReasons,
        Dictionary<string, int> ByCategory,
        Dictionary<string, int> ByAction);

    private sealed record IssueMetrics(
        Dictionary<string, int> ByIssue);
}
