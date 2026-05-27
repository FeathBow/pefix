using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanProfilesJson(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("artifact")] string Artifact);
