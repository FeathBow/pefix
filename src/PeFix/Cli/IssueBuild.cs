namespace PeFix.Cli;

internal sealed class IssueBuild
{
    public required DirectoryIssue[] Issues { get; init; }
    public required DirectoryIssue[] GateIssues { get; init; }
}
