namespace PeFix.Meta;

/// <summary>
/// The Unity scripting runtime a plugin was built against. BepInEx 5 is Mono
/// only; BepInEx 6 splits into Mono and IL2CPP, which load mutually exclusive
/// plugin sets. No version number encodes this, so it is name-derived only.
/// </summary>
public enum LoaderFlavor
{
    Unknown,
    Mono,
    Il2Cpp,
}
