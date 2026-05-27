namespace PeFix.Meta;

public readonly record struct VersionConflict(
    string AssemblyName,
    string Expected,
    string Actual,
    string ReferencedBy,
    string ProvidedBy);
