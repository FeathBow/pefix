using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record RedirJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("rows_patched")] int RowsPatched,
    [property: JsonPropertyName("targets")] MutationTargetJson[] Targets,
    [property: JsonPropertyName("repair_class")] string RepairClass,
    [property: JsonPropertyName("unverified_risks")] string[] UnverifiedRisks,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1)
{
    public const string RepairClassValue = global::PeFix.Cli.RepairClass.GuidedFix;
    public const string UnverifiedRiskText = "API/ABI compatibility and runtime load success are not proven.";

    public static string[] UnverifiedRiskList => [UnverifiedRiskText];
}
