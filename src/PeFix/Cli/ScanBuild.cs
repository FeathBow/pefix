using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";

    public static ScanView Build(ScanReport report, bool withJson)
    {
        ScanRel rel = new(report.Directory);
        var bepIndex = BepIndex.From(report.Results);
        var context = new ScanBuildContext(rel, withJson, bepIndex);
        ScanFile[] files = BuildFiles(report, context);
        DirConf[] conflicts = BuildConflicts(report, rel);
        DirMiss[] missingRefs = BuildMisses(report, rel);
        DirDup[] duplicateProviders = BuildDuplicates(report, rel);
        DirIssue[] issues = [
            .. BuildIssues(conflicts, missingRefs, duplicateProviders),
            .. BepIssues.Build(report.Results, rel, bepIndex)
        ];
        ScanStats stats = BuildStats(files, conflicts.Length > 0, issues);
        ScanJsonMeta? json = withJson
            ? BuildJson(new ScanJsonBuild
            {
                Files = files,
                Stats = stats,
                DuplicateCount = duplicateProviders.Length,
                Issues = issues
            })
            : null;
        return new ScanView(
            report.Directory,
            stats,
            files,
            conflicts,
            missingRefs,
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
            context.Rel.One(result.Path),
            Labels.CatText(result.Category),
            result.Status,
            InspectMap.CanPatch(result),
            InspectText.Summary(result),
            InspectMap.ActionCode(result),
            result.ReasonCode,
            context.WithJson ? InspectMap.Map(result, dep => context.BepIndex.Status(dep.Guid)) : null);
    }

    private static DirConf[] BuildConflicts(ScanReport report, ScanRel rel)
    {
        return [.. report.Conflicts
            .OrderBy(item => item.AssemblyName, StringComparer.Ordinal)
            .Select(conflict => new DirConf(
                conflict.AssemblyName,
                conflict.Expected,
                conflict.Actual,
                rel.One(conflict.ReferencedBy),
                rel.One(conflict.ProvidedBy)))];
    }

    private static DirMiss[] BuildMisses(ScanReport report, ScanRel rel)
    {
        return [.. report.MissingRefs
            .OrderBy(item => item.RefName, StringComparer.Ordinal)
            .Select(missingRef => new DirMiss(
                missingRef.RefName,
                missingRef.NeedVer,
                rel.One(missingRef.NeedBy)))];
    }

    private static DirDup[] BuildDuplicates(ScanReport report, ScanRel rel)
    {
        return [.. report.DupProviders
            .Select(dupProvider => new DirDup(
                dupProvider.AsmName,
                rel.Many(dupProvider.Files)))];
    }

    private static DirIssue[] BuildIssues(
        DirConf[] conflicts,
        DirMiss[] missingRefs,
        DirDup[] duplicateProviders)
    {
        var issues = new List<DirIssue>(conflicts.Length + missingRefs.Length + duplicateProviders.Length);
        AddConflicts(issues, conflicts);
        AddMisses(issues, missingRefs);
        AddDuplicates(issues, duplicateProviders);
        return [.. issues];
    }

    private static void AddConflicts(List<DirIssue> issues, DirConf[] conflicts)
    {
        foreach (DirConf conflict in conflicts)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.AsmConflict,
                Subject = conflict.Assembly,
                Summary = $"{conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}.",
                Files = [conflict.ReferencedBy, conflict.ProvidedBy]
            }));
        }
    }

    private static void AddMisses(List<DirIssue> issues, DirMiss[] missingRefs)
    {
        foreach (DirMiss missingRef in missingRefs)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.MissingRef,
                Subject = missingRef.Assembly,
                Summary = $"{missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found.",
                Files = [missingRef.RequiredBy]
            }));
        }
    }

    private static void AddDuplicates(List<DirIssue> issues, DirDup[] duplicateProviders)
    {
        foreach (DirDup duplicateProvider in duplicateProviders)
        {
            issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.DupProvider,
                Subject = duplicateProvider.Assembly,
                Summary = $"Multiple providers were found: {string.Join(", ", duplicateProvider.Files)}.",
                Files = duplicateProvider.Files
            }));
        }
    }

    private static ScanStats BuildStats(ScanFile[] files, bool hasConflict, DirIssue[] issues)
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

        foreach (DirIssue issue in issues)
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
        return new ScanJsonMeta(summary, gate);
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

    private static Dictionary<string, int> CountByIssue(DirIssue[] issues)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (DirIssue issue in issues)
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

    private readonly record struct ScanBuildContext(ScanRel Rel, bool WithJson, BepIndex BepIndex);

    private sealed record ScanJsonBuild
    {
        public required ScanFile[] Files { get; init; }
        public required ScanStats Stats { get; init; }
        public required int DuplicateCount { get; init; }
        public required DirIssue[] Issues { get; init; }
    }
}
