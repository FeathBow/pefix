using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PinCallJson(
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("type")] string DeclType,
    [property: JsonPropertyName("method")] string MethodName,
    [property: JsonPropertyName("entry")] string EntryName);
