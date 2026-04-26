using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PlanRollback(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("data")] JsonElement Data);
