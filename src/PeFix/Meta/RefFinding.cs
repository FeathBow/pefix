namespace PeFix.Meta;

public enum RefTier
{
    AssemblyRef,
    MemSurface,
    Provider,
    Reflection
}

public enum RefOutcome
{
    Missing,
    VersionConflict,
    MemberGap,
    DuplicateProvider,
    ReflectionMissing
}

public enum Confidence
{
    Gate,
    Advisory
}

public readonly record struct RefFinding(
    RefTier Tier,
    RefOutcome Resolution,
    Confidence Confidence,
    string ConsumerPath,
    string ReferenceName,
    string? TypeName,
    string? MemberName,
    int? ParameterCount,
    string? ExpectedVersion,
    string? ActualVersion,
    string? ProviderPath,
    string[]? ProviderPaths);
