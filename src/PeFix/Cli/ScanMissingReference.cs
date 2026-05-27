using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanMissingReference(
    [property: JsonPropertyName("assembly")] string Assembly,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("required_by")] string RequiredBy);
