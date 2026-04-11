namespace PeFix.Patch;

public readonly record struct BatchResult(
    string Directory,
    PatchResult[] Results,
    Refusal[] Refusals);
