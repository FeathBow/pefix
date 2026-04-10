namespace PeFix.Meta;

internal readonly record struct PeSnapshot(
    string Path,
    bool ValidPe,
    bool HasCliHeader,
    string? PeFormat,
    string? Machine,
    CliFlags CliFlags,
    Signals Signals);
