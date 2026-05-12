namespace PeFix.Meta;

public readonly record struct BepDep(
    string Guid,
    string? Range,
    bool Hard);
