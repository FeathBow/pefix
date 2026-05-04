namespace PeFix.Patch;

public readonly record struct PublicResult(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasDryRun,
    int OpsCount);
