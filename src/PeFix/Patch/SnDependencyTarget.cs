namespace PeFix.Patch;

internal sealed record SnDependencyTarget
{
    public required string Path { get; init; }
    public required byte[] Original { get; init; }
    public required SnDependencyWork Dependency { get; init; }
    public required IReadOnlyList<string> TargetNames { get; init; }
}
