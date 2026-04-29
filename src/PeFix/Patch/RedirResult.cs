namespace PeFix.Patch;

public readonly record struct RedirResult(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasDryRun,
    int RowsPatched);
