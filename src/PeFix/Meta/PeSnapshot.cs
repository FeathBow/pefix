namespace PeFix.Meta;

internal readonly record struct PeSnapshot(
    string Path,
    bool ValidPe,
    bool HasCliHeader,
    string? PeFormat,
    string? Machine,
    ManagedCorFlags ManagedCorFlags,
    Signals Signals,
    string[]? PInvokeDeps = null,
    string? Tfm = null,
    string? MetaVersion = null,
    string[]? OsPlatforms = null,
    AssemblyIdentity[]? AssemblyReferences = null,
    AssemblyIdentity? AssemblyDefinition = null,
    ReadyToRunInfo? ReadyToRun = null,
    bool IsTrimmable = false,
    bool HasNest = false,
    bool HasRefs = false,
    bool IsBundle = false,
    bool IsSatellite = false,
    BepInExMetadata? BepInEx = null);
