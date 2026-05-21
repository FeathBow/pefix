using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SnStripJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("was_patched")] bool WasPatched,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("signed_ivt")] bool SignedIvt,
    [property: JsonPropertyName("targets")] MutationTargetJson[] Targets,
    [property: JsonPropertyName("repair_class")] string RepairClass,
    [property: JsonPropertyName("unverified_risks")] string[] UnverifiedRisks,
    [property: JsonPropertyName("deps_patched")] int DepsPatched,
    [property: JsonPropertyName("deps")] SnDepJson[] Deps,
    [property: JsonPropertyName("dep_fails")] RefusalJson[] DepFails,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1)
{
    public const string RepairClassValue = global::PeFix.Cli.RepairClass.GuidedFix;
    public const string UnverifiedRiskText = "Assembly identity, signing/IVT compatibility, and runtime load success are not proven.";

    public static string[] UnverifiedRiskList => [UnverifiedRiskText];
}
