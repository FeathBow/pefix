using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanWriter
{
    public static string Render(ScanReport report, string commandName = "scan")
    {
        using var writer = new StringWriter();
        WriteHeader(writer, report, commandName);
        WriteCounts(writer, report);
        WriteGroups(writer, report);
        WriteConfs(writer, report);
        WriteMissing(writer, report);
        WriteHint(writer, report);
        return writer.ToString().TrimEnd();
    }

    private static void WriteHeader(StringWriter writer, ScanReport report, string commandName)
    {
        writer.WriteLine($"pefix {commandName} {Path.GetFileName(report.Directory)}");
        writer.WriteLine();
        writer.WriteLine($"  Summary: Scanned {report.Results.Length} candidate files. {NeedCount(report.Results)} require attention.");
        writer.WriteLine($"  Action:  {Action(report)}");
    }

    private static void WriteCounts(StringWriter writer, ScanReport report)
    {
        int compatible = report.Results.Count(r => r.Status == Status.Compatible);
        int fixable = report.Results.Count(r => r.Status == Status.Fixable);
        int cautioned = report.Results.Count(r => r.Status == Status.Cautioned);
        int @unsafe = report.Results.Count(r => r.Status == Status.Unsafe);
        int corrupt = report.Results.Count(r => r.Status == Status.Corrupt);
        writer.WriteLine($"  Counts:  compatible: {compatible}  fixable: {fixable}  cautioned: {cautioned}  unsafe: {@unsafe}  corrupt: {corrupt}");
    }

    private static void WriteGroups(StringWriter writer, ScanReport report)
    {
        if (report.Results.Length == 0)
        {
            writer.WriteLine();
            writer.WriteLine("  Groups:  No .dll, .exe, or .wasm files were found.");
            return;
        }

        foreach (IGrouping<string, Inspection>? group in report.Results
                     .GroupBy(result => Labels.CatText(result.Category), StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.WriteLine($"  Group: {group.Key}");
            foreach (Inspection result in group.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                string relativePath = Path.GetRelativePath(report.Directory, result.Path);
                writer.WriteLine($"    - {relativePath} [{Labels.StatusText(result.Status)}]");
            }
        }
    }

    private static void WriteConfs(StringWriter writer, ScanReport report)
    {
        if (report.Conflicts.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Version Conflicts ({report.Conflicts.Length}):");
        foreach (VerConflict conflict in report.Conflicts)
        {
            writer.WriteLine($"    - {conflict.AssemblyName}: {conflict.ReferencedBy} expects v{conflict.Expected}, but v{conflict.Actual} is provided by {conflict.ProvidedBy}");
        }
    }

    private static void WriteMissing(StringWriter writer, ScanReport report)
    {
        if (report.MissingRefs.Length == 0)
            return;

        writer.WriteLine();
        writer.WriteLine($"  Missing refs ({report.MissingRefs.Length}):");
        foreach (MissingRef missingRef in report.MissingRefs)
        {
            writer.WriteLine($"    - {missingRef.RefName}: {missingRef.NeedBy} expects v{missingRef.NeedVer}, but no provider was found");
        }
    }

    private static void WriteHint(StringWriter writer, ScanReport report)
    {
        if (report.Results.Length == 0)
        {
            return;
        }

        bool allOk = report.Results.All(r => r.Status == Status.Compatible)
            && report.Conflicts.Length == 0
            && report.MissingRefs.Length == 0;
        if (allOk)
        {
            writer.WriteLine();
            writer.WriteLine("  Hint: All assemblies use compatible headers. If loading still fails,");
            writer.WriteLine("        check host process architecture, loader configuration, or dependencies.");
        }
    }

    private static int NeedCount(Inspection[] results)
    {
        return results.Count(result => result.Status != Status.Compatible);
    }

    private static string Action(ScanReport report)
    {
        return Scanner.HasFixable(report)
            ? "Run pefix fix for entries marked fixable or cautioned."
            : "No fixable assemblies were found.";
    }
}
