using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SnStripJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("backup_path")] string? BackupPath,
    [property: JsonPropertyName("plan_path")] string? PlanPath,
    [property: JsonPropertyName("was_patched")] bool WasPatched,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("signed_ivt")] bool SignedIvt,
    [property: JsonPropertyName("deps_patched")] int DepsPatched,
    [property: JsonPropertyName("deps")] SnDepJson[] Deps,
    [property: JsonPropertyName("dep_fails")] RefusalJson[] DepFails);
