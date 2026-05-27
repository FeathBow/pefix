namespace PeFix.Meta;

public readonly record struct DuplicateProvider(
    string AssemblyName,
    string[] Files);
