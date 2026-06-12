using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record ClosureJson(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("entry_assemblies")] string[] Entries,
    [property: JsonPropertyName("unresolved_chains")] ChainJson[] Unresolved,
    [property: JsonPropertyName("cycle_chains")] ChainJson[] CycleChains,
    [property: JsonPropertyName("total_refs_walked")] int RefsWalked,
    [property: JsonPropertyName("provided_leaves")] int ProvidedLeaves,
    [property: JsonPropertyName("framework_leaves")] int FrameworkLeaves,
    [property: JsonPropertyName("tree")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TreeJson[]? Tree = null,
    [property: JsonPropertyName("schema_version"), JsonPropertyOrder(-1)] int SchemaVersion = 1);
