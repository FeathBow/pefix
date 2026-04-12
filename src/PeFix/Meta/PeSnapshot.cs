namespace PeFix.Meta;

internal readonly record struct PeSnapshot(
    string Path,
    bool ValidPe,
    bool HasCliHeader,
    string? PeFormat,
    string? Machine,
    CliFlags CliFlags,
    Signals Signals,
    string[]? PInvokeDeps = null,
    string? Tfm = null,
    string? MetaVersion = null,
    string[]? OsPlatforms = null,
    AsmRef[]? AssemblyRefs = null,
    AsmRef? AssemblyDef = null,
    R2RInfo? R2R = null,
    bool IsTrimmable = false,
    bool HasNest = false,
    bool HasRefs = false);
