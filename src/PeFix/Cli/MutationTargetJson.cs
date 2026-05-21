using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record MutationTargetJson(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("table")] string? Table,
    [property: JsonPropertyName("row")] int? Row,
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("offset")] long? Offset);
