using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("summary")] SummaryJson Summary,
    [property: JsonPropertyName("results")] InspectJson[] Results,
    [property: JsonPropertyName("conflicts")] ConflictJson[] Conflicts,
    [property: JsonPropertyName("missing_refs")] MissRefJson[] MissingRefs,
    [property: JsonPropertyName("dup_providers")] DupJson[] DupProviders);
