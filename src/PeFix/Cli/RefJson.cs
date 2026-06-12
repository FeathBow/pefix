using System.Text.Json.Serialization;

namespace PeFix.Cli;
internal sealed record RefJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("consumers")] RefUseJson[] Consumers);
