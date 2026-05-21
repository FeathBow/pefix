using PeFix.Plan;

namespace PeFix.Patch;

public readonly record struct RedirResult(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasDryRun,
    MutationOp[] Ops)
{
    public int RowsPatched => Ops.Length;
}
