using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record TreeJson(
    [property: JsonPropertyName("assembly")] string Assembly,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("children")] TreeJson[] Children);
