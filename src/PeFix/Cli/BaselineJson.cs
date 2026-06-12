using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BaselineJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("matched")] int Matched,
    [property: JsonPropertyName("new")] string[] Fresh,
    [property: JsonPropertyName("stale")] string[] Stale);
