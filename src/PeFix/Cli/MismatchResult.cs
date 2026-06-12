namespace PeFix.Cli;

internal readonly record struct MismatchResult(
    DirectoryIssue[] Issues,
    string[] BlockedPaths);
