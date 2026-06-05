using System.Text.Json.Serialization;

namespace PeFix.Cli;

internal sealed record IssueEvidence(
    [property: JsonPropertyName("declared_range")] string? DeclaredRange = null,
    [property: JsonPropertyName("present_version")] string? PresentVersion = null,
    [property: JsonPropertyName("provider_files")] string[]? ProviderFiles = null,
    [property: JsonPropertyName("entry_file")] string? EntryFile = null,
    [property: JsonPropertyName("request_chain")] string[]? RequestChain = null,
    [property: JsonPropertyName("missing_leaf")] string? MissingLeaf = null,
    [property: JsonPropertyName("type_name")] string? TypeName = null,
    [property: JsonPropertyName("member")] string? MemberName = null,
    [property: JsonPropertyName("parameter_count")] int? ParameterCount = null,
    [property: JsonPropertyName("matching_tier")] string? MatchingTier = null,
    [property: JsonPropertyName("provided_by")] string? ProviderFile = null)
{
    public static IssueEvidence ForProviderFiles(string[] providerFiles)
    {
        return new IssueEvidence(ProviderFiles: providerFiles);
    }

    public static IssueEvidence ForBepDependency(
        string? declaredRange,
        string? presentVersion = null,
        string[]? providerFiles = null)
    {
        return new IssueEvidence(
            DeclaredRange: declaredRange,
            PresentVersion: presentVersion,
            ProviderFiles: providerFiles);
    }

    public static IssueEvidence ForClosure(
        string entryFile,
        string[] requestChain,
        string missingLeaf)
    {
        return new IssueEvidence(
            EntryFile: entryFile,
            RequestChain: requestChain,
            MissingLeaf: missingLeaf);
    }

    public static IssueEvidence ForMissingMember(
        string typeName,
        string member,
        int parameterCount,
        string matchingTier,
        string providedBy)
    {
        return new IssueEvidence(
            TypeName: typeName,
            MemberName: member,
            ParameterCount: parameterCount,
            MatchingTier: matchingTier,
            ProviderFile: providedBy);
    }
}
