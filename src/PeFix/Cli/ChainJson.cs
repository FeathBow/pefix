using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ChainJson(
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("segments")] SegmentJson[] Segments);
