namespace PeFix.Patch;

public readonly record struct RedBatch(
    string Directory,
    RedirResult[] Results,
    Refusal[] Refusals);
