using PeFix.Cli;

namespace PeFix.Tests;

public sealed class BaselineTests
{
    [Fact]
    public void Lines_SortsAndDedupesAcrossIssues()
    {
        string[] lines = Baseline.Lines([
            Issue("missing_ref", "Missing.Core", ["B.dll", "A.dll"]),
            Issue("missing_ref", "Missing.Core", ["A.dll"]),
            Issue("bep_missing", "need.hard", ["Plugin.dll"])
        ]);

        Assert.Equal(
            [
                "bep_missing|need.hard|Plugin.dll",
                "missing_ref|Missing.Core|A.dll",
                "missing_ref|Missing.Core|B.dll"
            ],
            lines);
    }

    [Fact]
    public void Lines_UsesDashWhenIssueHasNoFiles()
    {
        string[] lines = Baseline.Lines([Issue("dup_provider", "Newtonsoft.Json", [])]);

        Assert.Equal(["dup_provider|Newtonsoft.Json|-"], lines);
    }

    [Fact]
    public void Parse_TrimsAndDropsBlankLines()
    {
        string[] parsed = Baseline.Parse(["  a|b|c  ", "", "   ", "d|e|f"]);

        Assert.Equal(["a|b|c", "d|e|f"], parsed);
    }

    [Fact]
    public void Diff_SplitsFreshStaleAndMatched()
    {
        BaselineDiff diff = Baseline.Diff(
            ["keep|x|A.dll", "new|y|B.dll"],
            ["keep|x|A.dll", "gone|z|C.dll"]);

        Assert.Equal(["new|y|B.dll"], diff.Fresh);
        Assert.Equal(["gone|z|C.dll"], diff.Stale);
        Assert.Equal(1, diff.Matched);
    }

    [Fact]
    public void Diff_EmptyBaselineMarksEverythingFresh()
    {
        BaselineDiff diff = Baseline.Diff(["a|b|C.dll"], []);

        Assert.Equal(["a|b|C.dll"], diff.Fresh);
        Assert.Empty(diff.Stale);
        Assert.Equal(0, diff.Matched);
    }

    private static DirectoryIssue Issue(string code, string subject, string[] files)
    {
        return new DirectoryIssue(
            code,
            subject,
            "summary",
            files,
            ["next step"],
            "assisted_fix",
            "hint",
            "pefix scan <path> --json",
            ["risk"]);
    }
}
