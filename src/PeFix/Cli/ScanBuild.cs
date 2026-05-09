using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanBuild
{
    private const string Pass = "pass";
    private const string Fail = "fail";
    private const string ConflictHint = "Align the directory to a single version for this assembly name. Remove the mismatched copy or install the version required by the referencing assembly.";
    private const string MissingHint = "Install the missing managed dependency into the scanned directory or restore the package that should provide it.";
    private const string DupHint = "Keep only one provider copy for this assembly name in the scanned directory. Remove or relocate duplicate copies.";

    public static ScanView Build(ScanReport report, bool withJson)
    {
        ScanRel rel = new(report.Directory);
        ScanFile[] files = BuildFiles(report, rel);
        DirConf[] conflicts = BuildConfs(report, rel);
        DirMiss[] missingRefs = BuildMisses(report, rel);
        DirDup[] dupProviders = BuildDups(report, rel);
        DirIssue[] issues = BuildIssues(conflicts, missingRefs, dupProviders);
        ScanStats stats = BuildStats(files, conflicts.Length > 0, issues);
        ScanJsonMeta? json = withJson ? BuildJson(files, stats, dupProviders.Length, issues) : null;
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

    private static ScanFile[] BuildFiles(ScanReport report, ScanRel rel)
    {
        return [.. report.Results.Select(result => BuildFile(result, rel))];
    }

    private static ScanFile BuildFile(Inspection result, ScanRel rel)
    {
        InspectJson json = InspectMap.Map(result);
        return new ScanFile(
            rel.One(result.Path),
            json.Category ?? Labels.CatText(result.Category),
            result.Status,
            InspectMap.CanPatch(result),
            InspectText.Summary(result),
            json);
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
                [ConflictHint]));
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
                [MissingHint]));
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
                [DupHint]));
        }
    }

    private static ScanStats BuildStats(ScanFile[] files, bool hasConflict, DirIssue[] issues)
    {
        var need = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int compatible = 0;
        int fixable = 0;
        int cautioned = 0;
        int @unsafe = 0;
        int corrupt = 0;
        bool hasFixable = false;

        foreach (ScanFile file in files)
        {
            CountStatus(file.Status, ref compatible, ref fixable, ref cautioned, ref @unsafe, ref corrupt);
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
            new ScanCounts(compatible, fixable, cautioned, @unsafe, corrupt),
            need.Count,
            hasFixable,
            hasConflict);
    }

    private static ScanJsonMeta BuildJson(ScanFile[] files, ScanStats stats, int dupCount, DirIssue[] issues)
    {
        ScanSummary summary = new(
            files.Length,
            stats.Counts.Compatible,
            stats.Counts.Fixable,
            stats.Counts.Cautioned,
            stats.Counts.Unsafe,
            stats.Counts.Corrupt,
            CountByCat(files),
            CountByAct(files),
            dupCount,
            issues.Length,
            CountByIssue(issues));
        string conflict = stats.HasConflict ? Fail : Pass;
        ScanGate gate = new(
            issues.Length == 0 ? Pass : Fail,
            conflict,
            conflict,
            issues.Length,
            [.. issues
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

    private static void CountStatus(
        Status status,
        ref int compatible,
        ref int fixable,
        ref int cautioned,
        ref int @unsafe,
        ref int corrupt)
    {
        switch (status)
        {
            case Status.Compatible:
                compatible++;
                break;
            case Status.Fixable:
                fixable++;
                break;
            case Status.Cautioned:
                cautioned++;
                break;
            case Status.Unsafe:
                @unsafe++;
                break;
            case Status.Corrupt:
                corrupt++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported inspection status.");
        }
    }

    private static void AddCount(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
    }
}
