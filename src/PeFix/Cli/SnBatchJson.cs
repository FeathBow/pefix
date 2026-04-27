using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SnBatchJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("results")] SnStripJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals,
    [property: JsonPropertyName("deps")] SnDepJson[] Deps);
