namespace PeFix.Meta;

internal readonly record struct FieldRefUse(
    string AssemblyName,
    string TypeName,
    string FieldName);
