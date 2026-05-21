using PeFix.Plan;

namespace PeFix.Patch;

public readonly record struct SnStripResult(
    string Path,
    string? BackupPath,
    string? PlanPath,
    bool WasSigned,
    SnStripOutcome Outcome,
    bool HadSignedIvt,
    MutationOp[] Ops,
    SnDependency[] Deps,
    Refusal[] DepFails)
{
    public bool WasPatched => Outcome == SnStripOutcome.Patched;
    public bool WasDryRun => Outcome == SnStripOutcome.DryRun;
    public int DepsPatched => Deps.Length;
}
