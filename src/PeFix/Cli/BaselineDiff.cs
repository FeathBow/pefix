namespace PeFix.Cli;

internal sealed record BaselineDiff(
    string[] Fresh,
    string[] Stale,
    int Matched);
