using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class InspectMap
{
    private static readonly Func<BepDep, BepDepState> UnknownBep = _ => BepDepState.Unknown;

    public static InspectJson Map(Inspection result)
    {
        return Map(result, UnknownBep);
    }

    public static InspectJson Map(Inspection result, Func<BepDep, BepDepState> depState)
    {
        RepairInfo repair = MapRepairInfo(result);
        return new InspectJson(
            result.Path,
            result.ValidPe,
            result.HasCliHeader,
            result.PeFormat,
            result.Machine,
            new CorFlagsJson(
                result.CliFlags.IlOnly,
                result.CliFlags.Required32Bit,
                result.CliFlags.Preferred32Bit,
                result.CliFlags.Signed),
            new SignalsJson(
                result.Signals.StrongName,
                result.Signals.HasPInvoke,
                result.Signals.IsRefAsm,
                result.Signals.IsMixedMode),
            result.Category is null ? null : Labels.CatText(result.Category),
            Labels.StatusText(result.Status),
            result.ReasonCode,
            ActionCode(result),
            repair.RepairClass,
            repair.RepairHint,
            result.PrimaryCause,
            result.RuntimeRisks,
            result.Warnings,
            result.NextSteps,
            result.LoadReqs,
            result.PInvokeDeps,
            result.Tfm,
            result.MetaVersion,
            result.OsPlatforms,
            result.AssemblyRefs?.Select(r => new AsmRefJson(r.Name, r.Version)).ToArray(),
            result.AssemblyDef is { } def ? new AsmRefJson(def.Name, def.Version) : null,
            MapBep(result.Bep, depState),
            result.HasR2R,
            result.IsTrimmable);
    }

    public static BepJson? MapBep(BepInfo? bep, Func<BepDep, BepDepState> depState)
    {
        if (!bep.HasValue)
            return null;

        return new BepJson([.. bep.Value.Plugins.Select(plugin => new BepPluginJson(
            plugin.Guid,
            plugin.Name,
            plugin.Version,
            [.. plugin.Deps.Select(dep => MapDep(dep, depState(dep)))]))]);
    }

    private static BepDepJson MapDep(BepDep dep, BepDepState state)
    {
        return new BepDepJson(
            dep.Guid,
            dep.Range,
            dep.Hard,
            Present(state),
            state is BepDepState.CaseMismatch);
    }

    private static bool? Present(BepDepState state)
    {
        return state switch
        {
            BepDepState.Present => true,
            BepDepState.Missing or BepDepState.CaseMismatch => false,
            _ => null
        };
    }

    public static RefusalJson MapRefusal(Refusal refusal)
    {
        return new(refusal.Path, refusal.Reason, Map(refusal.Before));
    }

    public static bool CanPatch(Inspection result)
    {
        return result.Status is Status.Fixable;
    }

    public static string ActionCode(Inspection result) => result.Status switch
    {
        Status.Compatible => "none",
        Status.Fixable => "fix",
        Status.Cautioned => "acknowledge",
        Status.Unsafe or Status.Corrupt => "blocked",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unsupported inspection status.")
    };

    public static RepairInfo MapRepairInfo(Inspection result)
    {
        if (string.Equals(result.ReasonCode, ReasonCode.NonPortable, StringComparison.Ordinal))
        {
            string repairClass = result.Status == Status.Fixable
                ? RepairClass.AutoFix
                : RepairClass.GuidedFix;
            return new RepairInfo(
                repairClass,
                NextStepOr(result, "Run pefix fix <path> --apply to rewrite the PE header."));
        }

        return result.ReasonCode switch
        {
            ReasonCode.R2R => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Verify the .NET runtime version matches the R2R compilation target before changing the artifact."),
            ReasonCode.Trimmable => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Verify required types are preserved before trimming."),
            ReasonCode.Bundle => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Use a non-bundled assembly or inspect the embedded assembly separately."),
            ReasonCode.PlatformApi => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Find a cross-platform alternative or target matching operating systems."),
            _ => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "No repair action is available for this diagnosis.")
        };
    }

    private static string NextStepOr(Inspection result, string fallback)
    {
        return result.NextSteps.Length > 0 ? result.NextSteps[0] : fallback;
    }
}
