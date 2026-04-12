using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ConflictJson(
    [property: JsonPropertyName("assembly")] string Assembly,
    [property: JsonPropertyName("expected")] string Expected,
    [property: JsonPropertyName("actual")] string Actual,
    [property: JsonPropertyName("referenced_by")] string ReferencedBy,
    [property: JsonPropertyName("provided_by")] string ProvidedBy);
