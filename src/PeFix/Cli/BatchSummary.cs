using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record BatchSummary(
    [property: JsonPropertyName("total_candidates")] int Total,
    [property: JsonPropertyName("patched")] int Patched,
    [property: JsonPropertyName("unchanged")] int Unchanged,
    [property: JsonPropertyName("dry_run")] int DryRun,
    [property: JsonPropertyName("refused")] int Refused);
