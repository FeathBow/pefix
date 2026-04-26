using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PlanMeta(
    [property: JsonPropertyName("sha")] string? Sha,
    [property: JsonPropertyName("host")] string? Host,
    [property: JsonPropertyName("ts")] DateTimeOffset Ts,
    [property: JsonPropertyName("url")] Uri? Url = null);
