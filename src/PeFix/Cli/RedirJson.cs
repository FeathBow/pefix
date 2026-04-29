using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RedirJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("rows_patched")] int RowsPatched);
