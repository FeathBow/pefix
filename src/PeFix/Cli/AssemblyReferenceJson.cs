using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record AssemblyReferenceJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);
