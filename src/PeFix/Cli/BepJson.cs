using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepJson(
    [property: JsonPropertyName("plugins")] BepPluginJson[] Plugins);
