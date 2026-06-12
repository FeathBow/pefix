using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RefUseJson(
    [property: JsonPropertyName("consumer")] string Consumer,
    [property: JsonPropertyName("requested_version")] string RequestedVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("provider_version")] string? ProviderVersion);
