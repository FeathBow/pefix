using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanOut
{
    public static string Render(ScanView view, bool includeReferences = false)
    {
        using var writer = new StringWriter();
        WriteHeader(writer, view);
        WriteCounts(writer, view);
        WriteIssues(writer, view);
        WriteGroups(writer, view);
        WriteConflicts(writer, view);
        WriteMissing(writer, view);
        WriteDuplicateProviders(writer, view);
        WriteReferences(writer, view, includeReferences);
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

    private static void WriteIssues(StringWriter writer, ScanView view)
    {
        writer.WriteLine();
        if (!view.HasIssues)
        {
            string detail = view.HasBlockingFiles
                ? "blocking file diagnostics are listed below."
                : "none found under supported static checks.";
            writer.WriteLine($"  Blocking Issues: {detail}");
            writer.WriteLine("  Static Boundary: Runtime load success is not certified.");
            return;
        }

        writer.WriteLine($"  Blocking Issues ({view.Issues.Length}):");
        foreach (DirectoryIssue issue in view.Issues)
            WriteIssue(writer, issue);

        writer.WriteLine("  Static Boundary: Findings are static evidence only; runtime load success is not certified.");
    }

    private static void WriteIssue(StringWriter writer, DirectoryIssue issue)
    {
        writer.WriteLine($"    - [{issue.Code}] {issue.Subject}: {issue.Summary}");
        writer.WriteLine($"      files: {string.Join(", ", issue.Files)}");
        writer.WriteLine($"      repair: {issue.RepairClass}");
        foreach (string step in issue.NextSteps)
            writer.WriteLine($"      next: {step}");

        writer.WriteLine($"      verify: {issue.VerifyCommand}");
        foreach (string risk in issue.UnverifiedRisks)
            writer.WriteLine($"      risk: {risk}");
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
                writer.WriteLine($"    - {file.ViewPath} [{Labels.StatusText(file.Status)}] reason={file.ReasonCode} action={file.ActionText}");
                if (file.NeedsWork)
                    writer.WriteLine($"      why: {file.ReasonText}");
            }
        }
    }

    private static void WriteConflicts(StringWriter writer, ScanView view)
    {
        if (view.Conflicts.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Version Conflicts ({view.Conflicts.Length}):");
        foreach (DirectoryConflict conflict in view.Conflicts)
            writer.WriteLine($"    - {conflict.Assembly}: {conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}");
    }

    private static void WriteMissing(StringWriter writer, ScanView view)
    {
        if (view.MissingReferences.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Missing references ({view.MissingReferences.Length}):");
        foreach (DirectoryMissingReference missingRef in view.MissingReferences)
            writer.WriteLine($"    - {missingRef.Assembly}: {missingRef.RequiredBy} expects v{missingRef.Version}, but no provider was found");
    }

    private static void WriteDuplicateProviders(StringWriter writer, ScanView view)
    {
        if (view.DuplicateProviders.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Duplicate providers ({view.DuplicateProviders.Length}):");
        foreach (DirectoryDuplicateProvider duplicateProvider in view.DuplicateProviders)
            writer.WriteLine($"    - {duplicateProvider.Assembly}: {string.Join(", ", duplicateProvider.Files)}");
    }

    private static void WriteReferences(
        StringWriter writer,
        ScanView view,
        bool includeReferences)
    {
        if (!includeReferences)
            return;

        writer.WriteLine();
        writer.WriteLine($"  References ({view.References.Length}):");
        PathRelativizer rel = new(view.Directory);
        foreach (IGrouping<string, RefEntry> group in ReferenceGroups(view))
            WriteReferenceGroup(writer, rel, group);
    }

    private static IEnumerable<IGrouping<string, RefEntry>> ReferenceGroups(ScanView view)
    {
        return view.References
            .GroupBy(entry => entry.ReferenceName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
    }

    private static void WriteReferenceGroup(
        StringWriter writer,
        PathRelativizer rel,
        IGrouping<string, RefEntry> group)
    {
        string status = RefStatText.Token(RefStatText.Highest(group));
        writer.WriteLine($"  Reference {group.Key} [{status}]");
        foreach (RefEntry entry in group)
            writer.WriteLine($"    - v{entry.RequestedVersion} by {rel.RelativePath(entry.ConsumerPath)} [{RefStatText.Token(entry.Status)}]{ProviderSuffix(entry, rel)}");
    }

    private static string ProviderSuffix(RefEntry entry, PathRelativizer rel)
    {
        if (entry.ProviderPath is null)
            return string.Empty;

        string provider = rel.RelativePath(entry.ProviderPath);
        return entry.ProviderVersion is null
            ? $" provider={provider}"
            : $" provider={provider} v{entry.ProviderVersion}";
    }

    private static void WriteBep(StringWriter writer, ScanView view)
    {
        DirectoryIssue[] issues = [.. view.Issues.Where(issue =>
            issue.Code is IssueCode.BepMissing or IssueCode.BepCasing)];
        if (issues.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  BepInEx deps ({issues.Length}):");
        foreach (DirectoryIssue issue in issues)
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

        if (view.HasBlockingFiles)
            return "Resolve blocking file diagnostics below before attempting runtime validation.";

        return view.Stats.HasFixable
            ? "Run pefix fix <path> --apply for entries marked fixable."
            : "No fixable assemblies were found.";
    }
}
