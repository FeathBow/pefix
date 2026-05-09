using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("summary")] ScanSummary Summary,
    [property: JsonPropertyName("results")] InspectJson[] Results,
    [property: JsonPropertyName("conflicts")] ScanConflict[] Conflicts,
    [property: JsonPropertyName("missing_refs")] ScanMissing[] MissingRefs,
    [property: JsonPropertyName("dup_providers")] ScanDup[] DupProviders,
    [property: JsonPropertyName("issues")] ScanIssue[] Issues,
    [property: JsonPropertyName("gate")] ScanGate Gate);
