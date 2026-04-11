using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RefusalJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("before")] InspectJson Before);
