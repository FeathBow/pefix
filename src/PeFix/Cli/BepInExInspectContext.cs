using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record BepInExInspectContext(
    BepInExProviderIndex ProviderIndex,
    string? ExplainState)
{
    public static BepInExInspectContext Empty { get; } = new(BepInExProviderIndex.Empty, null);

    public BepInExDependencyProviderPresence ProviderPresenceFor(BepInExDependencyMetadata dependency)
    {
        return ProviderIndex.ProviderPresenceFor(dependency.Guid);
    }
}
