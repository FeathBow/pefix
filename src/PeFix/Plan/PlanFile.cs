using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PlanFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("mvid")] string Mvid);
