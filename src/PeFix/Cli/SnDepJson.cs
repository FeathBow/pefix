using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SnDepJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("targets")] MutationTargetJson[] Targets);
