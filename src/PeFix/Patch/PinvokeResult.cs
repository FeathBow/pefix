namespace PeFix.Patch;

public readonly record struct PinvokeResult(
    string Path,
    PinvokeCall[] Calls);
