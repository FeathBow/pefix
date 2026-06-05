namespace PeFix.Meta;

public readonly record struct MemberRefGap(
    string AssemblyName,
    string TypeName,
    string MemberName,
    int ParameterCount,
    string MatchingTier,
    string ConsumerPath,
    string ProviderPath);
