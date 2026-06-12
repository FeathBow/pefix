using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanIssue(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("files")] string[] Files,
    [property: JsonPropertyName("next_steps")] string[] NextSteps,
    [property: JsonPropertyName("repair_class")] string RepairClass,
    [property: JsonPropertyName("repair_hint")] string RepairHint,
    [property: JsonPropertyName("verify_command")] string VerifyCommand,
    [property: JsonPropertyName("unverified_risks")] string[] UnverifiedRisks,
    [property: JsonPropertyName("evidence")] IssueEvidence? Evidence,
    [property: JsonPropertyName("in_static_ctor")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool StaticCtor = false);
