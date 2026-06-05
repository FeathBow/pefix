using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class InspectMap
{
    public static InspectJson Map(Inspection result)
    {
        return Map(result, new InspectInput(
            BepInExProviderIndex.Empty,
            null,
            LoaderTargetReader.FromReferences(result.AssemblyReferences)));
    }

    public static InspectJson Map(Inspection result, InspectInput input)
    {
        RepairInfo repair = RepairGuide.ForInspect(result);
        return new InspectJson(
            result.Path,
            result.ValidPe,
            result.HasCliHeader,
            result.PeFormat,
            result.Machine,
            new CorFlagsJson(
                result.ManagedCorFlags.IlOnly,
                result.ManagedCorFlags.Required32Bit,
                result.ManagedCorFlags.Preferred32Bit,
                result.ManagedCorFlags.Signed),
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
            result.AssemblyReferences?.Select(r => new AssemblyReferenceJson(r.Name, r.Version)).ToArray(),
            result.AssemblyDefinition is { } def ? new AssemblyReferenceJson(def.Name, def.Version) : null,
            MapBepInfo(result.BepInEx, input),
            result.HasReadyToRun,
            result.IsTrimmable);
    }

    private static BepInExJson? MapBepInfo(
        BepInExMetadata? bepInExMeta,
        InspectInput input)
    {
        if (!bepInExMeta.HasValue && input.ExplainState is null)
            return null;

        BepInExPluginMetadata[] plugins = bepInExMeta?.Plugins ?? [];
        return new BepInExJson(
            input.ExplainState ?? StateFromMetadata(plugins),
            [.. plugins.Select(plugin => new BepInExPluginJson(
                plugin.Guid,
                plugin.Name,
                plugin.Version,
                [.. plugin.Deps.Select(dependency => MapDependency(
                    dependency,
                    input.BepInExProviderIndex.MatchFor(dependency.Guid)))]))],
            GenerationToken(input.LoaderTarget.Generation),
            FlavorToken(input.LoaderTarget.Flavor),
            input.LoaderTarget.LoaderVersion?.ToString(),
            input.LoaderTarget.Reference);
    }

    private static string StateFromMetadata(BepInExPluginMetadata[] plugins)
    {
        return plugins.Length > 0 ? BepStateCode.Plugin : BepStateCode.Helper;
    }

    private static string? GenerationToken(LoaderGeneration generation) => generation switch
    {
        LoaderGeneration.BepInEx5 => "bepinex5",
        LoaderGeneration.BepInEx6 => "bepinex6",
        _ => null
    };

    private static string? FlavorToken(LoaderFlavor flavor) => flavor switch
    {
        LoaderFlavor.Mono => "mono",
        LoaderFlavor.Il2Cpp => "il2cpp",
        _ => null
    };

    private static BepInExDependencyJson MapDependency(
        BepInExDependencyMetadata dependency,
        BepInExProviderMatch match)
    {
        return new BepInExDependencyJson(
            dependency.Guid,
            dependency.Range,
            dependency.Hard,
            IsPresent(match),
            match is BepInExProviderMatch.CaseOnly);
    }

    private static bool? IsPresent(BepInExProviderMatch match)
    {
        return match switch
        {
            BepInExProviderMatch.Exact => true,
            BepInExProviderMatch.None => false,
            BepInExProviderMatch.CaseOnly => false,
            _ => null
        };
    }

    public static RefusalJson MapRefusal(Refusal refusal) => new(refusal.Path, refusal.Reason, Map(refusal.Before));

    public static bool CanPatch(Inspection result) => result.Status is Status.Fixable;

    public static string ActionCode(Inspection result) => result.Status switch
    {
        Status.Compatible => "none",
        Status.Fixable => "fix",
        Status.Cautioned => "acknowledge",
        Status.Unsafe or Status.Corrupt => "blocked",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unsupported inspection status.")
    };

    internal readonly record struct InspectInput(
        BepInExProviderIndex BepInExProviderIndex,
        string? ExplainState,
        LoaderTarget LoaderTarget);
}
