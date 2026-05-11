using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SegmentJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("kind")] string Kind);
