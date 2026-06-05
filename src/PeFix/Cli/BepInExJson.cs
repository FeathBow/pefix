using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepInExJson(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("plugins")] BepInExPluginJson[] Plugins,
    [property: JsonPropertyName("loader_generation")] string? LoaderGeneration = null,
    [property: JsonPropertyName("loader_flavor")] string? LoaderFlavor = null,
    [property: JsonPropertyName("loader_version")] string? LoaderVersion = null,
    [property: JsonPropertyName("loader_reference")] string? LoaderReference = null);
