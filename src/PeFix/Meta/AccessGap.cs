namespace PeFix.Meta;

public readonly record struct AccessGap(
    string AssemblyName,
    string TypeName,
    string? MemberName,
    int? ParameterCount,
    string ConsumerPath,
    string ProviderPath);
