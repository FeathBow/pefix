using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record PublicJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("ops_count")] int OpsCount,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
