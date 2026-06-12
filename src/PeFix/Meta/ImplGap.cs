namespace PeFix.Meta;

public readonly record struct ImplGap(
    string AssemblyName,
    string InterfaceName,
    string ClassName,
    string MemberName,
    int ParameterCount,
    string ConsumerPath,
    string ProviderPath);
