using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class ScanParts
{
    public required InspectJson[] Results { get; init; }
    public required ScanSummary Summary { get; init; }
    public required ScanGate Gate { get; init; }
    public required ScanProfile? Profile { get; init; }
    public RefEntry[]? References { get; init; }
}
