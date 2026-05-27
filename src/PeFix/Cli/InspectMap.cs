using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class InspectMap
{
    public static InspectJson Map(Inspection result)
    {
        return Map(result, BepInExInspectContext.Empty);
    }

    public static InspectJson Map(Inspection result, BepInExInspectContext bepInExContext)
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
            MapBep(result.BepInEx, bepInExContext),
            result.HasReadyToRun,
            result.IsTrimmable);
    }

    public static BepInExJson? MapBep(BepInExMetadata? bep, BepInExInspectContext context)
    {
        if (!bep.HasValue && context.ExplainState is null)
            return null;

        BepInExPluginMetadata[] plugins = bep?.Plugins ?? [];
        return new BepInExJson(context.ExplainState ?? BepInExExplainState.HelperLibrary, [.. plugins.Select(plugin => new BepInExPluginJson(
            plugin.Guid,
            plugin.Name,
            plugin.Version,
            [.. plugin.Deps.Select(dependency => MapDep(dependency, context.ProviderPresenceFor(dependency)))]))]);
    }

    private static BepInExDependencyJson MapDep(
        BepInExDependencyMetadata dependency,
        BepInExDependencyProviderPresence providerPresence)
    {
        return new BepInExDependencyJson(
            dependency.Guid,
            dependency.Range,
            dependency.Hard,
            IsProviderPresent(providerPresence),
            providerPresence is BepInExDependencyProviderPresence.CaseMismatchProviderFound);
    }

    private static bool? IsProviderPresent(BepInExDependencyProviderPresence providerPresence)
    {
        return providerPresence switch
        {
            BepInExDependencyProviderPresence.ExactProviderFound => true,
            BepInExDependencyProviderPresence.NoProviderFound => false,
            BepInExDependencyProviderPresence.CaseMismatchProviderFound => false,
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

}
