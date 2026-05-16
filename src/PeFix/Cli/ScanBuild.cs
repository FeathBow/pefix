using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";
    private const string ConflictHint = "Align the directory to one assembly version for this name.";
    private const string ConflictStep = "Remove the mismatched copy or install the version required by the referencing assembly.";
    private const string MissingHint = "Install or restore the missing managed dependency.";
    private const string MissingStep = "Install the missing managed dependency into the scanned directory or restore the package that should provide it.";
    private const string DupHint = "Keep one provider copy for this assembly name.";
    private const string DupStep = "Remove or relocate duplicate provider copies in the scanned directory.";
    private const string VerifyScan = "pefix scan <path> --json";

    public static ScanView Build(ScanReport report, bool withJson)
    {
        ScanRel rel = new(report.Directory);
        var bepIndex = BepIndex.From(report.Results);
        ScanBuildCtx ctx = new(rel, withJson, bepIndex);
        ScanFile[] files = BuildFiles(report, ctx);
        DirConf[] conflicts = BuildConfs(report, rel);
        DirMiss[] missingRefs = BuildMisses(report, rel);
        DirDup[] dupProviders = BuildDups(report, rel);
        DirIssue[] issues = [
            .. BuildIssues(conflicts, missingRefs, dupProviders),
            .. BepIssues.Build(report.Results, rel, bepIndex)
        ];
        ScanStats stats = BuildStats(files, conflicts.Length > 0, issues);
        ScanJsonMeta? json = withJson
            ? BuildJson(new ScanJsonBuild(files, stats, dupProviders.Length, issues))
            : null;
        return new ScanView(
            report.Directory,
            stats,
            files,
            conflicts,
            missingRefs,
            dupProviders,
            issues,
            json);
    }

    private static ScanFile[] BuildFiles(ScanReport report, ScanBuildCtx ctx)
    {
        return [.. report.Results.Select(result => BuildFile(result, ctx))];
    }

    private static ScanFile BuildFile(Inspection result, ScanBuildCtx ctx)
    {
        return new ScanFile(
            ctx.Rel.One(result.Path),
            Labels.CatText(result.Category),
            result.Status,
            InspectMap.CanPatch(result),
            InspectText.Summary(result),
            InspectMap.ActionCode(result),
            result.ReasonCode,
            ctx.WithJson ? InspectMap.Map(result, dep => ctx.BepIndex.Status(dep.Guid)) : null);
    }

    private static DirConf[] BuildConfs(ScanReport report, ScanRel rel)
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

    private static DirDup[] BuildDups(ScanReport report, ScanRel rel)
    {
        return [.. report.DupProviders
            .Select(dupProvider => new DirDup(
                dupProvider.AsmName,
                rel.Many(dupProvider.Files)))];
    }

    private static DirIssue[] BuildIssues(
        DirConf[] conflicts,
        DirMiss[] missingRefs,
        DirDup[] dupProviders)
    {
        var issues = new List<DirIssue>(conflicts.Length + missingRefs.Length + dupProviders.Length);
        AddConfs(issues, conflicts);
        AddMisses(issues, missingRefs);
        AddDups(issues, dupProviders);
        return [.. issues];
    }

    private static void AddConfs(List<DirIssue> issues, DirConf[] conflicts)
    {
        foreach (DirConf conflict in conflicts)
        {
            issues.Add(new DirIssue(
                IssueCode.AsmConflict,
                conflict.Assembly,
                $"{conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}.",
                [conflict.ReferencedBy, conflict.ProvidedBy],
                [ConflictStep],
                RepairClass.AssistedFix,
                ConflictHint,
                VerifyScan,
                ["API compatibility between aligned assembly versions is not proven."]));
        }
    }

    private static void AddMisses(List<DirIssue> issues, DirMiss[] missingRefs)
    {
        foreach (DirMiss missingRef in missingRefs)
        {
            issues.Add(new DirIssue(
                IssueCode.MissingRef,
                missingRef.Assembly,
                $"{missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found.",
                [missingRef.RequiredBy],
                [MissingStep],
                RepairClass.AssistedFix,
                MissingHint,
                VerifyScan,
                ["API compatibility and runtime load success are not proven."]));
        }
    }

    private static void AddDups(List<DirIssue> issues, DirDup[] dupProviders)
    {
        foreach (DirDup dupProvider in dupProviders)
        {
            issues.Add(new DirIssue(
                IssueCode.DupProvider,
                dupProvider.Assembly,
                $"Multiple providers were found: {string.Join(", ", dupProvider.Files)}.",
                dupProvider.Files,
                [DupStep],
                RepairClass.AssistedFix,
                DupHint,
                VerifyScan,
                ["Package ownership and intended provider selection are not proven."]));
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

    private static ScanJsonMeta BuildJson(ScanJsonBuild ctx)
    {
        ScanSummary summary = new(
            ctx.Files.Length,
            ctx.Stats.Counts.Compatible,
            ctx.Stats.Counts.Fixable,
            ctx.Stats.Counts.Cautioned,
            ctx.Stats.Counts.Unsafe,
            ctx.Stats.Counts.Corrupt,
            CountByCat(ctx.Files),
            CountByAct(ctx.Files),
            ctx.DupCount,
            ctx.Issues.Length,
            CountByIssue(ctx.Issues));
        string conflict = ctx.Stats.HasConflict ? Fail : Pass;
        ScanGate gate = new(
            ctx.Issues.Length == 0 ? Pass : Fail,
            conflict,
            ctx.Issues.Length,
            [.. ctx.Issues
                .Select(issue => issue.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)]);
        return new ScanJsonMeta(summary, gate);
    }

    private static Dictionary<string, int> CountByCat(ScanFile[] files)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ScanFile file in files)
            AddCount(counts, file.Category);
        return counts;
    }

    private static Dictionary<string, int> CountByAct(ScanFile[] files)
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

    private readonly record struct ScanBuildCtx(ScanRel Rel, bool WithJson, BepIndex BepIndex);

    private readonly record struct ScanJsonBuild(ScanFile[] Files, ScanStats Stats, int DupCount, DirIssue[] Issues);
}
