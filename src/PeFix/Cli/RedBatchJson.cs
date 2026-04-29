using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RedBatchJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("results")] RedirJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals);
