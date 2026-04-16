using System.Text.Json;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class JsonWriter
{
    public static string Render(Inspection result)
    {
        return JsonSerializer.Serialize(MapInspect(result), JsonContext.Default.InspectJson);
    }

    public static string Render(ScanReport report)
    {
        InspectJson[] models = report.Results.Select(MapInspect).ToArray();
        SummaryJson summary = MapSummary(report);
        ConflictJson[] conflicts = report.Conflicts.Select(MapConflict).ToArray();
        MissRefJson[] missingRefs = report.MissingRefs.Select(MapMissRef).ToArray();
        DupJson[] dupProviders = report.DupProviders.Select(MapDup).ToArray();
        var scanJson = new ScanJson(report.Directory, summary, models, conflicts, missingRefs, dupProviders);
        return JsonSerializer.Serialize(scanJson, JsonContext.Default.ScanJson);
    }

    public static string Render(PatchResult result)
    {
        return JsonSerializer.Serialize(CreateFix(result), JsonContext.Default.FixJson);
    }

    public static string Render(Refusal refusal)
    {
        return JsonSerializer.Serialize(MapRefusal(refusal), JsonContext.Default.RefusalJson);
    }

    public static string Render(BatchResult result)
    {
        return JsonSerializer.Serialize(CreateBatch(result), JsonContext.Default.BatchFixJson);
    }

    internal static InspectJson MapInspect(Inspection result)
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
            GetAction(result),
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

    private static string GetAction(Inspection result) => result.Status switch
    {
        Status.Compatible => "none",
        Status.Fixable => "fix",
        Status.Cautioned when result.Category == Category.Portability => "fix",
        Status.Cautioned => "acknowledge",
        _ => "blocked"
    };

    private static ConflictJson MapConflict(VerConflict conflict)
    {
        return new ConflictJson(
            conflict.AssemblyName,
            conflict.Expected,
            conflict.Actual,
            conflict.ReferencedBy,
            conflict.ProvidedBy);
    }

    private static MissRefJson MapMissRef(MissingRef missingRef)
    {
        return new MissRefJson(
            missingRef.RefName,
            missingRef.NeedVer,
            missingRef.NeedBy);
    }

    private static DupJson MapDup(DupProvider dupProvider)
    {
        return new DupJson(
            dupProvider.AsmName,
            dupProvider.Files);
    }

    private static SummaryJson MapSummary(ScanReport report)
    {
        Inspection[] results = report.Results;
        var byCategory = results
            .GroupBy(r => r.Category is null ? "unknown" : Labels.CatText(r.Category), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byAction = results
            .GroupBy(r => GetAction(r), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new SummaryJson(
            results.Length,
            results.Count(r => r.Status == Status.Compatible),
            results.Count(r => r.Status == Status.Fixable),
            results.Count(r => r.Status == Status.Cautioned),
            results.Count(r => r.Status == Status.Unsafe),
            results.Count(r => r.Status == Status.Corrupt),
            byCategory,
            byAction,
            report.DupProviders.Length);
    }

    private static FixJson CreateFix(PatchResult result)
    {
        string resultText = (result.DryRun, result.WasPatched) switch
        {
            (true, _) => "Dry run only",
            (false, true) => $"Patched {Path.GetFileName(result.Path)}",
            _ => "No changes were needed"
        };
        string verifyText = (result.DryRun, result.WasPatched) switch
        {
            (true, _) => "Skipped because no file was modified.",
            (false, false) => "Skipped because the assembly was already compatible.",
            _ => "Re-inspection passed. Assembly manifest was validated."
        };
        return new FixJson(
            result.Path,
            result.BackupPath,
            result.WasPatched,
            result.DryRun,
            resultText,
            verifyText,
            MapInspect(result.Before),
            MapInspect(result.After));
    }

    private static RefusalJson MapRefusal(Refusal refusal)
    {
        return new RefusalJson(
            refusal.Path,
            refusal.Reason,
            MapInspect(refusal.Before));
    }

    private static BatchFixJson CreateBatch(BatchResult result)
    {
        var summary = new BatchSummary(
            result.Results.Length + result.Refusals.Length,
            result.Results.Count(r => r.WasPatched),
            result.Results.Count(r => !r.WasPatched && !r.DryRun),
            result.Results.Count(r => r.DryRun),
            result.Refusals.Length);
        return new BatchFixJson(
            result.Directory,
            summary,
            result.Results.Select(CreateFix).ToArray(),
            result.Refusals.Select(MapRefusal).ToArray());
    }
}
