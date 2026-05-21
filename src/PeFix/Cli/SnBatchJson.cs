using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record SnBatchJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("results")] SnBatchResultJson[] Results,
    [property: JsonPropertyName("refusals")] RefusalJson[] Refusals,
    [property: JsonPropertyName("deps_patched")] int DepsPatched,
    [property: JsonPropertyName("deps")] SnDepJson[] Deps,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
