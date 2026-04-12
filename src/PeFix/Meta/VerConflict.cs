namespace PeFix.Meta;

public readonly record struct VerConflict(
    string AssemblyName,
    string Expected,     // version requested by ReferencedBy
    string Actual,       // version found in directory
    string ReferencedBy, // which DLL asked for it
    string ProvidedBy);  // which DLL provides the wrong version
