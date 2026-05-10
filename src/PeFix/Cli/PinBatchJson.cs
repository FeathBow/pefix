using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PinBatchJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("results")] PinvokeJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
