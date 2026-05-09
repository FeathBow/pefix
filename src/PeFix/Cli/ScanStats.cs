namespace PeFix.Cli;

internal sealed record ScanStats(
    ScanCounts Counts,
    int NeedCount,
    bool HasFixable,
    bool HasConflict);
