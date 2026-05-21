using PeFix.Patch;
using PeFix.Plan;

namespace PeFix.Cli;

internal static class MutationJsonMap
{
    public static RedirJson Map(RedirResult result)
    {
        return new RedirJson(
            result.Path,
            result.BackupPath,
            result.PlanPath,
            result.WasDryRun,
            result.RowsPatched,
            [.. result.Ops.Select(MapTarget)],
            RedirJson.RepairClassValue,
            RedirJson.UnverifiedRiskList);
    }

    public static SnStripJson Map(SnStripResult result)
    {
        return new SnStripJson(
            result.Path,
            result.BackupPath,
            result.PlanPath,
            SnStripOutcomeJson(result.Outcome),
            result.WasPatched,
            result.WasDryRun,
            result.HadSignedIvt,
            [.. result.Ops.Select(MapTarget)],
            SnStripRepairClass(result),
            SnStripJson.UnverifiedRiskList,
            result.DepsPatched,
            [.. result.Deps.Select(Map)],
            [.. result.DepFails.Select(InspectMap.MapRefusal)]);
    }

    public static SnDepJson Map(SnDependency dependency)
    {
        return new SnDepJson(
            dependency.Path,
            dependency.BackupPath,
            dependency.PlanPath,
            [.. dependency.Ops.Select(MapTarget)]);
    }

    public static PublicJson Map(PublicResult result)
    {
        return new PublicJson(
            result.Path,
            result.BackupPath,
            result.PlanPath,
            result.WasDryRun,
            result.OpsCount,
            [.. result.Ops.Select(MapTarget)],
            PublicJson.RepairClassValue,
            PublicJson.UnverifiedRiskList);
    }

    public static PinvokeJson Map(PinvokeResult result)
    {
        return new PinvokeJson(
            result.Path,
            [.. result.Calls.Select(MapPinvokeCall)]);
    }

    public static SnBatchResultJson MapBatchResult(SnStripResult result)
    {
        return new SnBatchResultJson(
            result.Path,
            result.BackupPath,
            result.PlanPath,
            SnStripOutcomeJson(result.Outcome),
            result.WasPatched,
            result.WasDryRun,
            result.HadSignedIvt,
            [.. result.Ops.Select(MapTarget)],
            SnStripRepairClass(result),
            SnStripJson.UnverifiedRiskList);
    }

    private static MutationTargetJson MapTarget(MutationOp op)
    {
        PlanTarget target = op.Target;
        return new MutationTargetJson(
            target.Kind,
            target.Table,
            target.Row,
            target.Handle,
            target.Offset);
    }

    private static PinCallJson MapPinvokeCall(PinvokeCall call)
    {
        return new PinCallJson(
            call.Module,
            call.DeclType,
            call.MethodName,
            call.EntryName);
    }

    private static string SnStripOutcomeJson(SnStripOutcome outcome) => outcome switch
    {
        SnStripOutcome.DryRun => "dry_run",
        SnStripOutcome.Patched => "patched",
        SnStripOutcome.Unsigned => "unsigned",
        SnStripOutcome.DepRefused => "dep_refused",
        _ => throw new InvalidOperationException($"Unknown snstrip outcome '{outcome}'.")
    };

    private static string SnStripRepairClass(SnStripResult result)
    {
        return result.Outcome == SnStripOutcome.Unsigned
            ? RepairClass.DiagnosticOnly
            : SnStripJson.RepairClassValue;
    }
}
