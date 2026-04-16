using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record DupJson(
    [property: JsonPropertyName("assembly")] string Assembly,
    [property: JsonPropertyName("files")] string[] Files);
