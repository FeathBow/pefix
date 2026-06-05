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

    public static string Render(ScanView view, ScanJsonParts json)
    {
        var output = new ScanJson(
            view.Directory,
            json.Summary,
            json.Results,
            [.. view.Conflicts.Select(MapConflict)],
            [.. view.MissingReferences.Select(MapMissing)],
            [.. view.DuplicateProviders.Select(MapDuplicate)],
            [.. view.Issues.Select(MapIssue)],
            MapProfile(json.Profile),
            json.Gate);
        return JsonSerializer.Serialize(output, JsonContext.Default.ScanJson);
    }

    public static string Render(PatchResult result)
    {
        return JsonSerializer.Serialize(CreateFix(result), JsonContext.Default.FixJson);
    }

    public static string Render(Refusal refusal)
    {
        return JsonSerializer.Serialize(InspectMap.MapRefusal(refusal), JsonContext.Default.RefusalJson);
    }

    public static string Render(BatchResult result)
    {
        return JsonSerializer.Serialize(CreateBatch(result), JsonContext.Default.BatchFixJson);
    }

    public static string Render(ClosureReport result)
    {
        return JsonSerializer.Serialize(ClosureMap.Map(result), JsonContext.Default.ClosureJson);
    }

    public static string Render(RedirResult result)
    {
        return JsonSerializer.Serialize(MutationJsonMap.Map(result), JsonContext.Default.RedirJson);
    }

    public static string Render(RedBatch batch)
    {
        var batchJson = new RedBatchJson(
            batch.Directory,
            [.. batch.Results.Select(MutationJsonMap.Map)],
            [.. batch.Refusals.Select(InspectMap.MapRefusal)]);
        return JsonSerializer.Serialize(batchJson, JsonContext.Default.RedBatchJson);
    }

    public static string Render(SnStripResult result)
    {
        return JsonSerializer.Serialize(MutationJsonMap.Map(result), JsonContext.Default.SnStripJson);
    }

    public static string Render(SnBatch batch)
    {
        var batchJson = new SnBatchJson(
            batch.Directory,
            batch.Outcome,
            batch.DryRun,
            [.. batch.Results.Select(MutationJsonMap.MapBatchResult)],
            [.. batch.Refusals.Select(InspectMap.MapRefusal)],
            batch.Deps.Length,
            [.. batch.Deps.Select(MutationJsonMap.Map)]);
        return JsonSerializer.Serialize(batchJson, JsonContext.Default.SnBatchJson);
    }

    public static string Render(PublicResult result)
    {
        return JsonSerializer.Serialize(MutationJsonMap.Map(result), JsonContext.Default.PublicJson);
    }

    public static string Render(PinvokeResult result)
    {
        return JsonSerializer.Serialize(MutationJsonMap.Map(result), JsonContext.Default.PinvokeJson);
    }

    public static string Render(PinBatch batch)
    {
        var batchJson = new PinBatchJson(
            batch.Directory,
            [.. batch.Results.Select(MutationJsonMap.Map)],
            [.. batch.Refusals.Select(InspectMap.MapRefusal)]);
        return JsonSerializer.Serialize(batchJson, JsonContext.Default.PinBatchJson);
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

    private static ScanConflict MapConflict(DirectoryConflict conflict)
    {
        return new ScanConflict(
            conflict.Assembly,
            conflict.Expected,
            conflict.Actual,
            conflict.ReferencedBy,
            conflict.ProvidedBy);
    }

    private static ScanMissingReference MapMissing(DirectoryMissingReference missingRef)
    {
        return new ScanMissingReference(
            missingRef.Assembly,
            missingRef.Version,
            missingRef.RequiredBy);
    }

    private static ScanDuplicateProvider MapDuplicate(DirectoryDuplicateProvider duplicateProvider)
    {
        return new ScanDuplicateProvider(duplicateProvider.Assembly, duplicateProvider.Files);
    }

    private static ScanIssue MapIssue(DirectoryIssue issue)
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
            issue.UnverifiedRisks,
            issue.Evidence);
    }

    private static ProfileJson? MapProfile(ScanProfile? profile)
    {
        if (profile is null)
            return null;

        LoaderTarget? target = profile.DeclaredLoaderTarget;
        return new ProfileJson(
            profile.Host.Name,
            profile.Artifact,
            target.HasValue ? GenerationToken(target.Value.Generation) : null,
            target.HasValue ? FlavorToken(target.Value.Flavor) : null);
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
            result.Refusals.Select(InspectMap.MapRefusal).ToArray());
    }
}
