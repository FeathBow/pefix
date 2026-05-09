using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanDup(
    [property: JsonPropertyName("assembly")] string Assembly,
    [property: JsonPropertyName("files")] string[] Files);
