using PeFix.Plan;

namespace PeFix.Patch;

public readonly record struct PublicResult(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasDryRun,
    MutationOp[] Ops)
{
    public int OpsCount => Ops.Length;
}
