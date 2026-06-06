using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RefUseJson(
    [property: JsonPropertyName("consumer")] string Consumer,
    [property: JsonPropertyName("requested_version")] string RequestedVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("provider_version")] string? ProviderVersion);

internal sealed record RefJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("consumers")] RefUseJson[] Consumers);

internal sealed record RefsJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("summary")] ScanSummary Summary,
    [property: JsonPropertyName("results")] InspectJson[] Results,
    [property: JsonPropertyName("conflicts")] ScanConflict[] Conflicts,
    [property: JsonPropertyName("missing_refs")] ScanMissingReference[] MissingReferences,
    [property: JsonPropertyName("dup_providers")] ScanDuplicateProvider[] DuplicateProviders,
    [property: JsonPropertyName("references")] RefJson[] References,
    [property: JsonPropertyName("issues")] ScanIssue[] Issues,
    [property: JsonPropertyName("profiles")] ProfileJson? Profile,
    [property: JsonPropertyName("gate")] ScanGate Gate,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
