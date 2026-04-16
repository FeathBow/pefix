using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SummaryJson(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("compatible")] int Compatible,
    [property: JsonPropertyName("fixable")] int Fixable,
    [property: JsonPropertyName("cautioned")] int Cautioned,
    [property: JsonPropertyName("unsafe")] int Unsafe,
    [property: JsonPropertyName("corrupt")] int Corrupt,
    [property: JsonPropertyName("by_category")] Dictionary<string, int> ByCategory,
    [property: JsonPropertyName("by_action")] Dictionary<string, int> ByAction,
    [property: JsonPropertyName("dup_providers")] int DupProviders);
