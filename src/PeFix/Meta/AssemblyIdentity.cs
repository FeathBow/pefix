using System.Text.Json.Serialization;

namespace PeFix.Meta;

public readonly record struct AssemblyIdentity(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);
