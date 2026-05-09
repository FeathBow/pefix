using PeFix.Meta;

namespace PeFix.Cli;

internal static class InspectMap
{
    public static InspectJson Map(Inspection result)
    {
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
            result.HasR2R,
            result.IsTrimmable);
    }

    public static bool CanPatch(Inspection result)
    {
        return result.Status is Status.Fixable or Status.Cautioned;
    }

    public static string ActionCode(Inspection result) => result.Status switch
    {
        Status.Compatible => "none",
        Status.Fixable => "fix",
        Status.Cautioned when result.Category == Category.Portability => "fix",
        Status.Cautioned => "acknowledge",
        Status.Unsafe or Status.Corrupt => "blocked",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unsupported inspection status.")
    };
}
