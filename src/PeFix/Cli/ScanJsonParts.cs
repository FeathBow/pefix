namespace PeFix.Cli;

internal sealed class ScanJsonParts
{
    public required InspectJson[] Results { get; init; }
    public required ScanSummary Summary { get; init; }
    public required ScanGate Gate { get; init; }
    public required ScanProfile? Profile { get; init; }
}
