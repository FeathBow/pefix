namespace PeFix.Meta;

public readonly record struct RefEntry(
    string ReferenceName,
    string RequestedVersion,
    string ConsumerPath,
    RefStatus Status,
    string? ProviderPath,
    string? ProviderVersion);
