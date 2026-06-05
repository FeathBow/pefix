using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("summary")] ScanSummary Summary,
    [property: JsonPropertyName("results")] InspectJson[] Results,
    [property: JsonPropertyName("conflicts")] ScanConflict[] Conflicts,
    [property: JsonPropertyName("missing_refs")] ScanMissingReference[] MissingReferences,
    [property: JsonPropertyName("dup_providers")] ScanDuplicateProvider[] DuplicateProviders,
    [property: JsonPropertyName("issues")] ScanIssue[] Issues,
    [property: JsonPropertyName("profiles")] ProfileJson? Profile,
    [property: JsonPropertyName("gate")] ScanGate Gate,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
