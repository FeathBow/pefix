using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class ScanBuildCtx
{
    public required ScanReport Report { get; init; }
    public required ScanProfile? Profile { get; init; }
    public required PathRelativizer Rel { get; init; }
    public required BepInExProviderIndex BepInExProviderIndex { get; init; }
    public required IReadOnlyDictionary<string, LoaderTarget> LoaderByPath { get; init; }
}
