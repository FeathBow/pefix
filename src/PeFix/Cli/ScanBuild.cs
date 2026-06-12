using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";

    public static ScanResult Build(
        ScanReport report,
        bool withJson,
        ScanProfile? profile = null,
        bool includeReferences = false)
    {
        HostProfile hostProfile = profile?.Host ?? HostProfile.Default;
        bool publishDirProfile = IsPublishDirProfile(profile);
        return Build(
            report,
            withJson,
            RefEvidence.Collect(report, hostProfile, publishDirProfile),
            profile,
            includeReferences);
    }

    internal static ScanResult Build(
        ScanReport report,
        bool withJson,
        RefFinding[] findings,
        ScanProfile? profile = null,
        bool includeReferences = false)
    {
        PathRelativizer rel = new(report.Directory);
        ScanBuildCtx ctx = CreateCtx(report, profile, rel);
        RefEntry[] references = BuildReferences(report, profile, includeReferences);
        RefEntry[]? jsonReferences = includeReferences ? references : null;
        BepInExExplainResult bepInExExplain = BuildBepInExExplain(ctx);
        ScanFile[] files = BuildFiles(report.Results, rel);
        RefFinding[] refs = RefRows.Build(findings, rel);
        IssueBuild issueBuild = BuildIssues(findings, ctx.Rel, bepInExExplain);
        ScanMetrics metrics = MetricBuild.Build(new MetricInput
        {
            Files = files,
            Issues = issueBuild.Issues,
            GateIssues = issueBuild.GateIssues,
            HasConflict = RefRows.Count(refs, RefOutcome.VersionConflict) > 0,
            DuplicateCount = RefRows.Count(refs, RefOutcome.DuplicateProvider)
        });
        ScanView view = new(
            report.Directory,
            metrics.Stats,
            files,
            refs,
            issueBuild.Issues,
            issueBuild.GateIssues,
            references);
        ScanParts? json = withJson ? BuildJson(ctx, bepInExExplain, metrics, jsonReferences) : null;
        return new ScanResult(view, json);
    }

    private static RefEntry[] BuildReferences(
        ScanReport report,
        ScanProfile? profile,
        bool includeReferences)
    {
        return includeReferences
            ? RefInventory.Collect(report.Results, profile?.Host ?? HostProfile.Default)
            : [];
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

    private static IssueBuild BuildIssues(
        RefFinding[] findings,
        PathRelativizer rel,
        BepInExExplainResult bepInExExplain)
    {
        RefFinding[] gateFindings = [.. findings.Where(item => item.Confidence == Confidence.Gate)];
        return new IssueBuild
        {
            Issues = [.. DirectoryIssueBuilder.Build(findings, rel), .. bepInExExplain.Issues],
            GateIssues = [.. DirectoryIssueBuilder.Build(gateFindings, rel), .. bepInExExplain.Issues]
        };
    }

    private static ScanParts BuildJson(
        ScanBuildCtx ctx,
        BepInExExplainResult bepInExExplain,
        ScanMetrics metrics,
        RefEntry[]? references)
    {
        return BuildScanJson(new ScanInput
        {
            Results = ctx.Report.Results,
            Profile = ctx.Profile,
            BepInExProviderIndex = ctx.BepInExProviderIndex,
            BepInExExplain = bepInExExplain,
            LoaderByPath = ctx.LoaderByPath,
            Metrics = metrics,
            References = references
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

    private static InspectJson[] BuildInspectJson(ScanInput input)
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

    private static bool IsPublishDirProfile(ScanProfile? profile)
    {
        return profile is not null
            && string.Equals(profile.Artifact, ProfileParser.PublishDir, StringComparison.Ordinal);
    }

    internal static ScanParts BuildScanJson(ScanInput input)
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
            GateStatus(input.Metrics.GateIssueCount == 0 && input.Metrics.BlockingFileCount == 0),
            GateStatus(!input.Metrics.Stats.HasConflict),
            input.Metrics.GateIssueCount,
            input.Metrics.GateIssueCodes,
            input.Metrics.BlockingFileCount,
            input.Metrics.BlockingFileReasons);
        return new ScanParts
        {
            Results = fileResults,
            Summary = summary,
            Gate = gate,
            Profile = input.Profile,
            References = input.References
        };
    }

    private static string GateStatus(bool passed) => passed ? Pass : Fail;

}
