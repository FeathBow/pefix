using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BepInExJson(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("plugins")] BepInExPluginJson[] Plugins);
