using System.Text.Json.Serialization;

namespace PeFix.Meta;

internal sealed class DepsJson
{
    [JsonPropertyName("targets")]
    public Dictionary<string, Dictionary<string, DepsLibJson>>? Targets { get; set; }
}
