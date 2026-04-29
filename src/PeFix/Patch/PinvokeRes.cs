namespace PeFix.Patch;

public readonly record struct PinvokeRes(
    string Path,
    PinvokeCall[] Calls);
