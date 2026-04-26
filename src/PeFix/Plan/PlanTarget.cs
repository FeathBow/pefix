using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PlanTarget(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("table")] string? Table = null,
    [property: JsonPropertyName("row")] int? Row = null,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("offset")] long? Offset = null);
