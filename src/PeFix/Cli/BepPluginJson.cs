using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepPluginJson(
    [property: JsonPropertyName("guid")] string Guid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("deps")] BepDepJson[] Deps);
