using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeFix.Meta;

internal sealed class DepsLibJson
{
    [JsonPropertyName("runtime")]
    public Dictionary<string, JsonElement>? Runtime { get; set; }
}
