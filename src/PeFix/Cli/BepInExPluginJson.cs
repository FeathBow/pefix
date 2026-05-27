using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepInExPluginJson(
    [property: JsonPropertyName("guid")] string Guid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("deps")] BepInExDependencyJson[] Deps);
