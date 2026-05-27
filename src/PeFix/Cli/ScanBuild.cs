using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";

    public static ScanView Build(ScanReport report, bool withJson, ScanProfiles? profiles = null)
    {
        ScanPathRelativizer rel = new(report.Directory);
        BepInExProviderIndex bepInExProviderIndex = BepInExProviderIndex.From(report.Results);
        ClosureReport closure = ClosureGraph.Build(report.Results, report.Directory);
        BepInExExplainResult bepExplain = BepInExExplain.Explain(
            report.Results,
            rel,
            bepInExProviderIndex,
            closure);
        var context = new ScanBuildContext(rel, withJson, bepInExProviderIndex, bepExplain);
        ScanFile[] files = BuildFiles(report, context);
        DirectoryConflict[] conflicts = BuildConflicts(report, rel);
        DirectoryMissingReference[] missingReferences = BuildMisses(report, rel);
        DirectoryDuplicateProvider[] duplicateProviders = BuildDuplicates(report, rel);
        DirectoryIssue[] issues = [
            .. DirectoryIssueBuilder.Build(conflicts, missingReferences, duplicateProviders),
            .. bepExplain.Issues
        ];
        ScanStats stats = BuildStats(files, conflicts.Length > 0, issues);
        ScanJsonMeta? json = withJson
            ? BuildJson(new ScanJsonBuild
            {
                Files = files,
                Stats = stats,
                DuplicateCount = duplicateProviders.Length,
                Issues = issues,
                Profiles = profiles
            })
            : null;
        return new ScanView(
            report.Directory,
            stats,
            files,
            conflicts,
            missingReferences,
            duplicateProviders,
            issues,
            json);
    }

    private static ScanFile[] BuildFiles(ScanReport report, ScanBuildContext context)
    {
        return [.. report.Results.Select(result => BuildFile(result, context))];
    }

    private static ScanFile BuildFile(Inspection result, ScanBuildContext context)
    {
        return new ScanFile(
            context.Rel.RelativePath(result.Path),
            Labels.CatText(result.Category),
            result.Status,
            InspectMap.CanPatch(result),
            InspectText.Summary(result),
            InspectMap.ActionCode(result),
            result.ReasonCode,
            context.WithJson
                ? InspectMap.Map(
                    result,
                    new BepInExInspectContext(
                        context.BepInExProviderIndex,
                        context.BepInExExplain.StateForFile(result.Path)))
                : null);
    }

    private static DirectoryConflict[] BuildConflicts(ScanReport report, ScanPathRelativizer rel)
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

    private static DirectoryMissingReference[] BuildMisses(ScanReport report, ScanPathRelativizer rel)
    {
        return [.. report.MissingReferences
            .OrderBy(item => item.ReferenceName, StringComparer.Ordinal)
            .Select(missingRef => new DirectoryMissingReference(
                missingRef.ReferenceName,
                missingRef.RequiredVersion,
                rel.RelativePath(missingRef.RequiredBy)))];
    }

    private static DirectoryDuplicateProvider[] BuildDuplicates(ScanReport report, ScanPathRelativizer rel)
    {
        return [.. report.DuplicateProviders
            .Select(duplicateProvider => new DirectoryDuplicateProvider(
                duplicateProvider.AssemblyName,
                rel.RelativePaths(duplicateProvider.Files)))];
    }

    private static ScanStats BuildStats(ScanFile[] files, bool hasConflict, DirectoryIssue[] issues)
    {
        var need = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanCounts counts = new(0, 0, 0, 0, 0);
        bool hasFixable = false;

        foreach (ScanFile file in files)
        {
            counts = CountStatus(counts, file.Status);
            if (file.NeedsWork)
                need.Add(file.ViewPath);

            if (file.CanPatch)
                hasFixable = true;
        }

        foreach (DirectoryIssue issue in issues)
        {
            foreach (string file in issue.Files)
                need.Add(file);
        }

        return new ScanStats(
            counts,
            need.Count,
            hasFixable,
            hasConflict);
    }

    private static ScanJsonMeta BuildJson(ScanJsonBuild context)
    {
        ScanSummary summary = new(
            context.Files.Length,
            context.Stats.Counts.Compatible,
            context.Stats.Counts.Fixable,
            context.Stats.Counts.Cautioned,
            context.Stats.Counts.Unsafe,
            context.Stats.Counts.Corrupt,
            CountByCategory(context.Files),
            CountByAction(context.Files),
            context.DuplicateCount,
            context.Issues.Length,
            CountByIssue(context.Issues));
        string conflict = context.Stats.HasConflict ? Fail : Pass;
        ScanGate gate = new(
            context.Issues.Length == 0 ? Pass : Fail,
            conflict,
            context.Issues.Length,
            [.. context.Issues
                .Select(issue => issue.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)]);
        return new ScanJsonMeta(summary, gate, context.Profiles);
    }

    private static Dictionary<string, int> CountByCategory(ScanFile[] files)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ScanFile file in files)
            AddCount(counts, file.Category);
        return counts;
    }

    private static Dictionary<string, int> CountByAction(ScanFile[] files)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ScanFile file in files)
            AddCount(counts, file.Action);
        return counts;
    }

    private static Dictionary<string, int> CountByIssue(DirectoryIssue[] issues)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (DirectoryIssue issue in issues)
            AddCount(counts, issue.Code);
        return counts;
    }

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

    private readonly record struct ScanBuildContext(
        ScanPathRelativizer Rel,
        bool WithJson,
        BepInExProviderIndex BepInExProviderIndex,
        BepInExExplainResult BepInExExplain);

    private sealed record ScanJsonBuild
    {
        public required ScanFile[] Files { get; init; }
        public required ScanStats Stats { get; init; }
        public required int DuplicateCount { get; init; }
        public required DirectoryIssue[] Issues { get; init; }
        public required ScanProfiles? Profiles { get; init; }
    }
}
