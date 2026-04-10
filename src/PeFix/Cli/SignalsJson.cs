using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SignalsJson(
    [property: JsonPropertyName("has_strong_name")] bool StrongName,
    [property: JsonPropertyName("has_pinvoke")] bool HasPInvoke,
    [property: JsonPropertyName("is_reference_assembly")] bool IsRefAsm,
    [property: JsonPropertyName("is_mixed_mode")] bool IsMixedMode);
