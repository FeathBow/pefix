using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record IssueEvidence(
    [property: JsonPropertyName("declared_range")] string? DeclaredRange = null,
    [property: JsonPropertyName("present_version")] string? PresentVersion = null,
    [property: JsonPropertyName("provider_files")] string[]? ProviderFiles = null,
    [property: JsonPropertyName("entry_file")] string? EntryFile = null,
    [property: JsonPropertyName("request_chain")] string[]? RequestChain = null,
    [property: JsonPropertyName("missing_leaf")] string? MissingLeaf = null);
