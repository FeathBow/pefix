namespace PeFix.Meta;

/// <summary>
/// The BepInEx loader generation a plugin was built against. This is a
/// structural partition, not a version bump: BepInEx 5 is the monolithic
/// <c>BepInEx</c> assembly, BepInEx 6 is the componentized <c>BepInEx.Core</c>
/// family. Detected from which loader assembly the plugin references, which
/// survives repacked or wrong assembly versions.
/// </summary>
public enum LoaderGeneration
{
    Unknown,
    BepInEx5,
    BepInEx6,
}
