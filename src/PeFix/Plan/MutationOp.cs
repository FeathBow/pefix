using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record MutationOp(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("target")] PlanTarget Target,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("after")] string After);
