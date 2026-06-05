using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ProfileJson(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("artifact")] string Artifact,
    [property: JsonPropertyName("declared_loader_generation")] string? DeclaredLoaderGeneration = null,
    [property: JsonPropertyName("declared_loader_flavor")] string? DeclaredLoaderFlavor = null);
