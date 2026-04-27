namespace PeFix.Patch;

public readonly record struct SnStripRes(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasPatched,
    bool WasDryRun,
    bool HadSignedIvt,
    int DepsPatched,
    SnDep[] Deps,
    Refusal[] DepFails);
