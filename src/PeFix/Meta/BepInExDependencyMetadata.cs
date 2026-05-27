namespace PeFix.Meta;

public readonly record struct BepInExDependencyMetadata(
    string Guid,
    string? Range,
    bool Hard);
