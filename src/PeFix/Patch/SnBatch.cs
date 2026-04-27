namespace PeFix.Patch;

public readonly record struct SnBatch(
    string Directory,
    SnStripRes[] Results,
    Refusal[] Refusals,
    SnDep[] Deps);
