namespace PeFix.Cli;

internal sealed record ScanStats(
    ScanCounts Counts,
    bool HasFixable,
    bool HasConflict);
