using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BatchFixJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("summary")] BatchSummary Summary,
    [property: JsonPropertyName("results")] FixJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals);
