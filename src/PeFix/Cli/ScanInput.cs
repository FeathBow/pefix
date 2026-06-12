using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class ScanInput
{
    public required Inspection[] Results { get; init; }
    public required ScanProfile? Profile { get; init; }
    public required BepInExProviderIndex BepInExProviderIndex { get; init; }
    public required BepInExExplainResult BepInExExplain { get; init; }
    public required IReadOnlyDictionary<string, LoaderTarget> LoaderByPath { get; init; }
    public required ScanMetrics Metrics { get; init; }
    public required RefEntry[]? References { get; init; }
}
