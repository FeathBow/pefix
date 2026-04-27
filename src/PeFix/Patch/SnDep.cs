namespace PeFix.Patch;

public readonly record struct SnDep(
    string Path,
    string? BackupPath,
    string? PlanPath);
