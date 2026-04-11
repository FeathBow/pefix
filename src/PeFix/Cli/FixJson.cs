using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record FixJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("was_patched")] bool WasPatched,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("verify")] string Verify,
    [property: JsonPropertyName("before")] InspectJson Before,
    [property: JsonPropertyName("after")] InspectJson After);
