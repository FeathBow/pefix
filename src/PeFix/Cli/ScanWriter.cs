using PeFix.Meta;

namespace PeFix.Cli;

internal static class ScanWriter
{
    public static string Render(ScanReport report, string commandName = "scan")
    {
        using var writer = new StringWriter();
        WriteHeader(writer, report, commandName);
        WriteGroups(writer, report);
        return writer.ToString().TrimEnd();
    }

    private static void WriteHeader(StringWriter writer, ScanReport report, string commandName)
    {
        writer.WriteLine($"pefix {commandName} {Path.GetFileName(report.Directory)}");
        writer.WriteLine();
        writer.WriteLine($"  Summary: Scanned {report.Results.Length} candidate files. {NeedCount(report.Results)} require attention.");
        writer.WriteLine($"  Action:  {Action(report)}");
    }

    private static void WriteGroups(StringWriter writer, ScanReport report)
    {
        if (report.Results.Length == 0)
        {
            writer.WriteLine();
            writer.WriteLine("  Groups:  No .dll or .exe files were found.");
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

    private static int NeedCount(Inspection[] results)
    {
        return results.Count(result => result.Status != Status.Compatible);
    }

    private static string Action(ScanReport report)
    {
        return Scanner.HasFixable(report)
            ? "Run pefix fix for entries marked fixable or fixable-with-warnings."
            : "No fixable assemblies were found.";
    }
}
