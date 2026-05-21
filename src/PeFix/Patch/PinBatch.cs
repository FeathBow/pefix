namespace PeFix.Patch;

public readonly record struct PinBatch(
    string Directory,
    PinvokeResult[] Results,
    Refusal[] Refusals);
