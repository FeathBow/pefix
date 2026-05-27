namespace PeFix.Cli;

internal sealed record ScanJsonMeta(
    ScanSummary Summary,
    ScanGate Gate,
    ScanProfiles? Profiles);
