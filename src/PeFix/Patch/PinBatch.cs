namespace PeFix.Patch;

public readonly record struct PinBatch(
    string Directory,
    PinvokeRes[] Results,
    Refusal[] Refusals);
