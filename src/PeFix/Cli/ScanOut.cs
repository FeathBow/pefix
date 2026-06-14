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
        WriteReferences(writer, view, includeReferences);
        WriteHint(writer, view);
        return writer.ToString().TrimEnd();
    }

    private static void WriteHeader(StringWriter writer, ScanView view)
    {
        writer.WriteLine($"pefix {Path.GetFileName(view.Directory)}");
        writer.WriteLine();
        int fileNeed = view.Files.Length - view.Stats.Counts.Compatible;
        int issueCount = view.Issues.Length;
        writer.WriteLine($"  Summary: Scanned {Plural.Count(view.Files.Length, "candidate file")}. {fileNeed} need attention, {Plural.Count(issueCount, "directory issue")}.");
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
            string boundary = view.HasBlockingFiles
                ? "  Static Boundary: blocking file diagnostics are listed below; runtime load success is not certified."
                : "  Static Boundary: no supported static issue found; runtime load success is not certified.";
            writer.WriteLine(boundary);
            return;
        }

        writer.WriteLine($"  Issues ({view.Issues.Length}):");
        foreach (IGrouping<string, DirectoryIssue> group in view.Issues.GroupBy(issue => issue.Code, StringComparer.Ordinal))
            WriteIssueGroup(writer, group);

        writer.WriteLine("  Verify: pefix scan <path> --json");
        writer.WriteLine("  Static Boundary: Findings are static evidence only; runtime load success is not certified.");
        writer.WriteLine("  Exit:   scan exits 0 by default; add --fail-on-issue to fail CI on these.");
    }

    private static void WriteIssueGroup(StringWriter writer, IGrouping<string, DirectoryIssue> group)
    {
        // One code shares one repair class, next steps, and risks, so they print once per
        // group rather than once per instance; Distinct guards the rare per-issue variant.
        foreach (DirectoryIssue issue in group)
        {
            writer.WriteLine($"    - [{issue.Code}] {issue.Subject}: {issue.Summary}");
            writer.WriteLine($"      files: {string.Join(", ", issue.Files)}");
            if (issue.StaticCtor)
                writer.WriteLine("      note: static constructor: TypeInitializationException, type becomes unusable");
        }

        writer.WriteLine($"      repair: {group.First().RepairClass}");
        foreach (string step in group.SelectMany(issue => issue.NextSteps).Distinct(StringComparer.Ordinal))
            writer.WriteLine($"      next: {step}");

        foreach (string risk in group.SelectMany(issue => issue.UnverifiedRisks).Distinct(StringComparer.Ordinal))
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
            : "None needed.";
    }
}
