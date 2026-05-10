using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ScanGate(
    [property: JsonPropertyName("integrity")] string Integrity,
    [property: JsonPropertyName("version_conflict")] string VersionConflict,
    [property: JsonPropertyName("issue_count")] int IssueCount,
    [property: JsonPropertyName("issue_codes")] string[] IssueCodes);
