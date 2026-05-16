namespace PeFix.Cli;

internal static class ScanOut
{
    public static string Render(ScanView view)
    {
        using var writer = new StringWriter();
        WriteHeader(writer, view);
        WriteCounts(writer, view);
        WriteGroups(writer, view);
        WriteConfs(writer, view);
        WriteMissing(writer, view);
        WriteDup(writer, view);
        WriteBep(writer, view);
        WriteNexts(writer, view);
        WriteHint(writer, view);
        return writer.ToString().TrimEnd();
    }

    private static void WriteHeader(StringWriter writer, ScanView view)
    {
        writer.WriteLine($"pefix {Path.GetFileName(view.Directory)}");
        writer.WriteLine();
        writer.WriteLine($"  Summary: Scanned {view.Files.Length} candidate files. {view.Stats.NeedCount} require attention.");
        writer.WriteLine($"  Action:  {ActionText(view)}");
    }

    private static void WriteCounts(StringWriter writer, ScanView view)
    {
        writer.WriteLine($"  Counts:  compatible: {view.Stats.Counts.Compatible}  fixable: {view.Stats.Counts.Fixable}  cautioned: {view.Stats.Counts.Cautioned}  unsafe: {view.Stats.Counts.Unsafe}  corrupt: {view.Stats.Counts.Corrupt}  issues: {view.Issues.Length}");
    }

    private static void WriteGroups(StringWriter writer, ScanView view)
    {
        if (view.Files.Length == 0)
        {
            writer.WriteLine();
            writer.WriteLine("  Groups:  No .dll, .exe, or .wasm files were found.");
            return;
        }

        foreach (IGrouping<string, ScanFile>? group in view.Files
                     .GroupBy(file => file.Category, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.WriteLine($"  Group: {group.Key}");
            foreach (ScanFile file in group)
            {
                writer.WriteLine($"    - {file.ViewPath} [{Labels.StatusText(file.Status)}] reason={file.ReasonCode} action={file.Action}");
                if (file.NeedsWork)
                    writer.WriteLine($"      why: {file.Why}");
            }
        }
    }

    private static void WriteConfs(StringWriter writer, ScanView view)
    {
        if (view.Conflicts.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Version Conflicts ({view.Conflicts.Length}):");
        foreach (DirConf conflict in view.Conflicts)
            writer.WriteLine($"    - {conflict.Assembly}: {conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}");
    }

    private static void WriteMissing(StringWriter writer, ScanView view)
    {
        if (view.MissingRefs.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Missing refs ({view.MissingRefs.Length}):");
        foreach (DirMiss missingRef in view.MissingRefs)
            writer.WriteLine($"    - {missingRef.Assembly}: {missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found");
    }

    private static void WriteDup(StringWriter writer, ScanView view)
    {
        if (view.DupProviders.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Dup providers ({view.DupProviders.Length}):");
        foreach (DirDup dupProvider in view.DupProviders)
            writer.WriteLine($"    - {dupProvider.Assembly}: {string.Join(", ", dupProvider.Files)}");
    }

    private static void WriteBep(StringWriter writer, ScanView view)
    {
        DirIssue[] issues = [.. view.Issues.Where(issue =>
            issue.Code is IssueCode.BepMissing or IssueCode.BepCasing)];
        if (issues.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  BepInEx deps ({issues.Length}):");
        foreach (DirIssue issue in issues)
            writer.WriteLine($"    - {issue.Summary}");
    }

    private static void WriteNexts(StringWriter writer, ScanView view)
    {
        if (!view.HasIssues)
            return;

        string[] steps = [.. view.Issues
            .SelectMany(issue => issue.NextSteps)
            .Distinct(StringComparer.Ordinal)];
        if (steps.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine("  Next Steps:");
        foreach (string step in steps)
            writer.WriteLine($"    - {step}");
    }

    private static void WriteHint(StringWriter writer, ScanView view)
    {
        if (view.Files.Length == 0)
            return;

        bool allOk = view.Stats.Counts.Compatible == view.Files.Length && !view.HasIssues;
        if (allOk)
        {
            writer.WriteLine();
            writer.WriteLine("  Hint: All assemblies use compatible headers. If loading still fails,");
            writer.WriteLine("        check host process architecture, loader configuration, or dependencies.");
        }
    }

    private static string ActionText(ScanView view)
    {
        if (view.HasIssues)
        {
            return view.Stats.HasFixable
                ? "Resolve directory issues below, then run pefix fix <path> --apply for entries marked fixable."
                : "Resolve directory issues below before attempting runtime validation.";
        }

        return view.Stats.HasFixable
            ? "Run pefix fix <path> --apply for entries marked fixable."
            : "No fixable assemblies were found.";
    }
}
