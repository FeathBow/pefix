using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record InspectJson(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("valid_pe")] bool ValidPe,
    [property: JsonPropertyName("has_cli_header")] bool HasCliHeader,
    [property: JsonPropertyName("pe_format")] string? PeFormat,
    [property: JsonPropertyName("machine")] string? Machine,
    [property: JsonPropertyName("cor_flags")] CorFlagsJson CorFlags,
    [property: JsonPropertyName("signals")] SignalsJson Signals,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("primary_cause")] string PrimaryCause,
    [property: JsonPropertyName("runtime_risks")] string[] RuntimeRisks,
    [property: JsonPropertyName("warnings")] string[] Warnings,
    [property: JsonPropertyName("next_steps")] string[] NextSteps,
    [property: JsonPropertyName("load_reqs")] string? LoadReqs,
    [property: JsonPropertyName("pinvoke_deps")] string[]? PInvokeDeps);
