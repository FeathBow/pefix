using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PinvokeJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("calls")] PinCallJson[] Calls,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
