using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record CorFlagsJson(
    [property: JsonPropertyName("il_only")] bool IlOnly,
    [property: JsonPropertyName("required_32bit")] bool Required32Bit,
    [property: JsonPropertyName("preferred_32bit")] bool Preferred32Bit,
    [property: JsonPropertyName("signed")] bool Signed);
