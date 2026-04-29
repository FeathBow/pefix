using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PinvokeJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("calls")] PinCallJson[] Calls);
