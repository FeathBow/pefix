using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ClosureJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("entry_assemblies")] string[] Entries,
    [property: JsonPropertyName("unresolved_chains")] ChainJson[] Unresolved,
    [property: JsonPropertyName("cycle_chains")] ChainJson[] CycleChains,
    [property: JsonPropertyName("total_refs_walked")] int RefsWalked,
    [property: JsonPropertyName("framework_leaves")] int HostLeaves,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);

internal sealed record ChainJson(
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("segments")] SegmentJson[] Segments);

internal sealed record SegmentJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("kind")] string Kind);
