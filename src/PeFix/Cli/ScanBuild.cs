using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";

    public static ScanResult Build(ScanReport report, bool withJson, ScanProfile? profile = null)
    {
        PathRelativizer rel = new(report.Directory);
        ScanBuildCtx ctx = CreateCtx(report, profile, rel);
        BepInExExplainResult bepInExExplain = BuildBepInExExplain(ctx);
        ScanFile[] files = BuildFiles(report.Results, rel);
        DirectoryConflict[] conflicts = BuildConflicts(report, rel);
        DirectoryMissingReference[] missingReferences = BuildMissingRefs(report, rel);
        DirectoryDuplicateProvider[] duplicateProviders = BuildDuplicates(report, rel);
        DirectoryIssue[] issues = BuildIssues(new ScanDirectoryIssueInput
        {
            Report = report,
            Rel = rel,
            Conflicts = conflicts,
            MissingReferences = missingReferences,
            DuplicateProviders = duplicateProviders,
            BepInExExplain = bepInExExplain
        });
        ScanMetrics metrics = BuildMetrics(new ScanMetricInput
        {
            Files = files,
            Issues = issues,
            HasConflict = conflicts.Length > 0,
            DuplicateCount = duplicateProviders.Length
        });
        ScanView view = new(
            report.Directory,
            metrics.Stats,
            files,
            conflicts,
            missingReferences,
            duplicateProviders,
            issues);
        ScanJsonParts? json = withJson ? BuildJson(ctx, bepInExExplain, metrics) : null;
        return new ScanResult(view, json);
    }

    private static ScanBuildCtx CreateCtx(
        ScanReport report,
        ScanProfile? profile,
        PathRelativizer rel)
    {
        return new ScanBuildCtx
        {
            Report = report,
            Profile = profile,
            Rel = rel,
            BepInExProviderIndex = BepInExProviderIndex.From(report.Results),
            LoaderByPath = LoaderTargetReader.FromInspections(report.Results)
        };
    }

    private static BepInExExplainResult BuildBepInExExplain(ScanBuildCtx ctx)
    {
        if (!ShouldExplainBepInEx(ctx.Profile))
            return BepInExExplainResult.Empty;

        ClosureReport closure = ClosureGraph.Build(ctx.Report.Results, ctx.Report.Directory, ctx.Profile?.Host);
        return BepInExExplain.Explain(
            ctx.Report.Results,
            ctx.Rel,
            ctx.BepInExProviderIndex,
            closure,
            ctx.Profile?.DeclaredLoaderTarget,
            ctx.LoaderByPath);
    }

    private static DirectoryIssue[] BuildIssues(ScanDirectoryIssueInput input)
    {
        return [
            .. DirectoryIssueBuilder.Build(new IssueSources
            {
                Conflicts = input.Conflicts,
                MissingReferences = input.MissingReferences,
                DuplicateProviders = input.DuplicateProviders,
                MemberRefGaps = input.Report.MemberRefGaps,
                Rel = input.Rel
            }),
            .. input.BepInExExplain.Issues
        ];
    }

    private static ScanJsonParts BuildJson(
        ScanBuildCtx ctx,
        BepInExExplainResult bepInExExplain,
        ScanMetrics metrics)
    {
        return BuildScanJson(new ScanJsonInput
        {
            Results = ctx.Report.Results,
            Profile = ctx.Profile,
            BepInExProviderIndex = ctx.BepInExProviderIndex,
            BepInExExplain = bepInExExplain,
            LoaderByPath = ctx.LoaderByPath,
            Metrics = metrics
        });
    }

    private static ScanFile[] BuildFiles(
        Inspection[] results,
        PathRelativizer rel)
    {
        return [.. results.Select(result => BuildFile(result, rel))];
    }

    private static ScanFile BuildFile(
        Inspection result,
        PathRelativizer rel)
    {
        return new ScanFile(
            rel.RelativePath(result.Path),
            Labels.CatText(result.Category),
            result.Status,
            InspectMap.CanPatch(result),
            InspectText.Summary(result),
            InspectMap.ActionCode(result),
            result.ReasonCode);
    }

    private static DirectoryConflict[] BuildConflicts(ScanReport report, PathRelativizer rel)
    {
        return [.. report.Conflicts
            .OrderBy(item => item.AssemblyName, StringComparer.Ordinal)
            .Select(conflict => new DirectoryConflict(
                conflict.AssemblyName,
                conflict.Expected,
                conflict.Actual,
                rel.RelativePath(conflict.ReferencedBy),
                rel.RelativePath(conflict.ProvidedBy)))];
    }

    private static DirectoryMissingReference[] BuildMissingRefs(ScanReport report, PathRelativizer rel)
    {
        return [.. report.MissingReferences
            .OrderBy(item => item.ReferenceName, StringComparer.Ordinal)
            .Select(missingRef => new DirectoryMissingReference(
                missingRef.ReferenceName,
                missingRef.RequiredVersion,
                rel.RelativePath(missingRef.RequiredBy)))];
    }

    private static DirectoryDuplicateProvider[] BuildDuplicates(ScanReport report, PathRelativizer rel)
    {
        return [.. report.DuplicateProviders
            .Select(duplicateProvider => new DirectoryDuplicateProvider(
                duplicateProvider.AssemblyName,
                rel.RelativePaths(duplicateProvider.Files)))];
    }

    private static ScanMetrics BuildMetrics(ScanMetricInput input)
    {
        var need = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanCounts counts = new(0, 0, 0, 0, 0);
        bool hasFixable = false;
        int blockingFileCount = 0;
        var byCategory = new Dictionary<string, int>(StringComparer.Ordinal);
        var byAction = new Dictionary<string, int>(StringComparer.Ordinal);
        var byIssue = new Dictionary<string, int>(StringComparer.Ordinal);
        var blockingReasons = new HashSet<string>(StringComparer.Ordinal);

        foreach (ScanFile file in input.Files)
        {
            counts = CountStatus(counts, file.Status);
            AddCount(byCategory, file.Category);
            AddCount(byAction, file.ActionText);
            if (file.NeedsWork)
                need.Add(file.ViewPath);

            if (file.CanPatch)
                hasFixable = true;

            if (file.Status is Status.Unsafe or Status.Corrupt)
            {
                blockingFileCount++;
                blockingReasons.Add(file.ReasonCode);
            }
        }

        foreach (DirectoryIssue issue in input.Issues)
        {
            AddCount(byIssue, issue.Code);
            foreach (string file in issue.Files)
                need.Add(file);
        }

        return new ScanMetrics
        {
            FileCount = input.Files.Length,
            DuplicateCount = input.DuplicateCount,
            Stats = new ScanStats(
                counts,
                need.Count,
                hasFixable,
                input.HasConflict),
            ByCategory = byCategory,
            ByAction = byAction,
            ByIssue = byIssue,
            BlockingFileCount = blockingFileCount,
            BlockingFileReasons = [.. blockingReasons.OrderBy(reason => reason, StringComparer.Ordinal)]
        };
    }

    private static InspectJson[] BuildInspectJson(ScanJsonInput input)
    {
        return [.. input.Results.Select(result => InspectMap.Map(
            result,
            new InspectMap.InspectInput(
                input.BepInExProviderIndex,
                input.BepInExExplain.StateForFile(result.Path),
                input.LoaderByPath[result.Path])))];
    }

    private static bool ShouldExplainBepInEx(ScanProfile? profile)
    {
        return profile is null || string.Equals(profile.Artifact, ProfileParser.PluginFolder, StringComparison.Ordinal);
    }

    internal static ScanJsonParts BuildScanJson(ScanJsonInput input)
    {
        InspectJson[] fileResults = BuildInspectJson(input);
        int issueCount = input.Metrics.ByIssue.Values.Sum();
        ScanSummary summary = new(
            input.Metrics.FileCount,
            input.Metrics.Stats.Counts.Compatible,
            input.Metrics.Stats.Counts.Fixable,
            input.Metrics.Stats.Counts.Cautioned,
            input.Metrics.Stats.Counts.Unsafe,
            input.Metrics.Stats.Counts.Corrupt,
            input.Metrics.ByCategory,
            input.Metrics.ByAction,
            input.Metrics.DuplicateCount,
            issueCount,
            input.Metrics.ByIssue);
        ScanGate gate = new(
            GateStatus(issueCount == 0 && input.Metrics.BlockingFileCount == 0),
            GateStatus(!input.Metrics.Stats.HasConflict),
            issueCount,
            [.. input.Metrics.ByIssue.Keys.OrderBy(code => code, StringComparer.Ordinal)],
            input.Metrics.BlockingFileCount,
            input.Metrics.BlockingFileReasons);
        return new ScanJsonParts
        {
            Results = fileResults,
            Summary = summary,
            Gate = gate,
            Profile = input.Profile
        };
    }

    private static string GateStatus(bool passed) => passed ? Pass : Fail;

    private static ScanCounts CountStatus(ScanCounts counts, Status status)
    {
        return status switch
        {
            Status.Compatible => counts with { Compatible = counts.Compatible + 1 },
            Status.Fixable => counts with { Fixable = counts.Fixable + 1 },
            Status.Cautioned => counts with { Cautioned = counts.Cautioned + 1 },
            Status.Unsafe => counts with { Unsafe = counts.Unsafe + 1 },
            Status.Corrupt => counts with { Corrupt = counts.Corrupt + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported inspection status.")
        };
    }

    private static void AddCount(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

}
