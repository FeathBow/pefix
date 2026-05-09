namespace PeFix.Meta;

public readonly record struct VerConflict(
    string AssemblyName,
    string Expected,
    string Actual,
    string ReferencedBy,
    string ProvidedBy);
