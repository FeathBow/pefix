namespace PeFix.Meta;

internal readonly record struct MethodRefUse(
    string AssemblyName,
    string TypeName,
    string MemberName,
    int ParameterCount);
