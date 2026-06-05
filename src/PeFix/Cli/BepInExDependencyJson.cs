using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepInExDependencyJson(
    [property: JsonPropertyName("guid")] string Guid,
    [property: JsonPropertyName("range")] string? Range,
    [property: JsonPropertyName("hard")] bool Hard,
    [property: JsonPropertyName("present")] bool? Present = null,
    [property: JsonPropertyName("case_mismatch")] bool CaseMismatch = false);
