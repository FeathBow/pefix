using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PlanTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);
