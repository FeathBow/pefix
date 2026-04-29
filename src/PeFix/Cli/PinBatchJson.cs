using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PinBatchJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("results")] PinvokeJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals);
