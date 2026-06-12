namespace PeFix.Meta;

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
    string[]? ProviderPaths,
    bool StaticCtor = false);
