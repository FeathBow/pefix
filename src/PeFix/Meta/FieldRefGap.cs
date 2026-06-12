namespace PeFix.Meta;

public readonly record struct FieldRefGap(
    string AssemblyName,
    string TypeName,
    string FieldName,
    string MatchingTier,
    string ConsumerPath,
    string ProviderPath);
