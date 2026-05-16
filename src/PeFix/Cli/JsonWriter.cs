using System.Text.Json;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class JsonWriter
{
    public static string Render(Inspection result)
    {
        return JsonSerializer.Serialize(InspectMap.Map(result), JsonContext.Default.InspectJson);
    }

    public static string Render(ScanView view)
    {
        ScanJsonMeta json = view.Json ?? throw new InvalidOperationException("Scan JSON metadata was not built.");
        var scanJson = new ScanJson(
            view.Directory,
            json.Summary,
            [.. view.Files.Select(file => file.Json!)],
            [.. view.Conflicts.Select(MapConf)],
            [.. view.MissingRefs.Select(MapMiss)],
            [.. view.DupProviders.Select(MapDup)],
            [.. view.Issues.Select(MapIssue)],
            json.Gate);
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

    private static FixJson CreateFix(PatchResult result)
    {
        string resultText = (result.DryRun, result.WasPatched) switch
        {
            (true, _) => FixResult.DryRun,
            (false, true) => FixResult.Patched,
            _ => FixResult.Unchanged
        };
        string verifyText = (result.DryRun, result.WasPatched) switch
        {
            (true, _) => FixVerify.Skipped,
            (false, false) => FixVerify.Skipped,
            _ => FixVerify.Ok
        };
        return new FixJson(
            result.Path,
            result.BackupPath,
            result.WasPatched,
            result.DryRun,
            resultText,
            verifyText,
            InspectMap.Map(result.Before),
            InspectMap.Map(result.After));
    }

    private static RefusalJson MapRefusal(Refusal refusal)
    {
        return InspectMap.MapRefusal(refusal);
    }

    private static ScanConflict MapConf(DirConf conflict)
    {
        return new ScanConflict(
            conflict.Assembly,
            conflict.Expected,
            conflict.Actual,
            conflict.ReferencedBy,
            conflict.ProvidedBy);
    }

    private static ScanMissing MapMiss(DirMiss missingRef)
    {
        return new ScanMissing(
            missingRef.Assembly,
            missingRef.Version,
            missingRef.RequiredBy);
    }

    private static ScanDup MapDup(DirDup dupProvider)
    {
        return new ScanDup(dupProvider.Assembly, dupProvider.Files);
    }

    private static ScanIssue MapIssue(DirIssue issue)
    {
        return new ScanIssue(
            issue.Code,
            issue.Subject,
            issue.Summary,
            issue.Files,
            issue.NextSteps,
            issue.RepairClass,
            issue.RepairHint,
            issue.VerifyCommand,
            issue.UnverifiedRisks);
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
