using PeFix.Plan;

namespace PeFix.Patch;

public readonly record struct SnDependency(
    string Path,
    string? BackupPath,
    string? PlanPath,
    MutationOp[] Ops);
