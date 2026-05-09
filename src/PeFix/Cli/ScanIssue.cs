using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanIssue(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("files")] string[] Files,
    [property: JsonPropertyName("next_steps")] string[] NextSteps);
