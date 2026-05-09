namespace PeFix.Cli;

internal readonly record struct ScanCounts(
    int Compatible,
    int Fixable,
    int Cautioned,
    int Unsafe,
    int Corrupt);
