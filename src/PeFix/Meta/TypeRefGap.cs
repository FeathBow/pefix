namespace PeFix.Meta;

public readonly record struct TypeRefGap(
    string AssemblyName,
    string TypeName,
    string ConsumerPath,
    string ProviderPath);
