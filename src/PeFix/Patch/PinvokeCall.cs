namespace PeFix.Patch;

public readonly record struct PinvokeCall(
    string Module,
    string DeclType,
    string MethodName,
    string EntryName);
