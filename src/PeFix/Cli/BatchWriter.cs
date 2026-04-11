using PeFix.Patch;

namespace PeFix.Cli;

internal static class BatchWriter
{
    public static string Render(BatchResult result)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"pefix fix {Path.GetFileName(result.Directory)}");
        writer.WriteLine();
        writer.WriteLine($"  Summary: {Summary(result)}");
        writer.WriteLine($"  Action:  {Action(result)}");
        WriteSection(writer, "Patched", result.Results.Where(item => item.WasPatched).Select(item => FormatPath(result.Directory, item.Path)).ToArray());
        WriteSection(writer, "Unchanged", result.Results.Where(item => !item.WasPatched && !item.DryRun).Select(item => FormatPath(result.Directory, item.Path)).ToArray());
        WriteSection(writer, "Dry Run", result.Results.Where(item => item.DryRun).Select(item => FormatPath(result.Directory, item.Path)).ToArray());
        WriteSection(writer, "Refused", result.Refusals.Select(item => $"{FormatPath(result.Directory, item.Path)}: {item.Reason}").ToArray());
        return writer.ToString().TrimEnd();
    }

    private static string Summary(BatchResult result)
    {
        int totalCount = result.Results.Length + result.Refusals.Length;
        if (totalCount == 0)
        {
            return "No .dll or .exe files were found.";
        }

        string[] parts = new[]
        {
            $"Patched {result.Results.Count(item => item.WasPatched)}",
            $"unchanged {result.Results.Count(item => !item.WasPatched && !item.DryRun)}",
            $"dry-run {result.Results.Count(item => item.DryRun)}",
            $"refused {result.Refusals.Length}"
        };
        return $"Processed {totalCount} candidate files. {string.Join(", ", parts)}.";
    }

    private static string Action(BatchResult result)
    {
        if (result.Refusals.Length > 0)
        {
            return "Review refused files and only rerun with --force when the warnings are acceptable.";
        }

        if (result.Results.Any(item => item.DryRun))
        {
            return "Review the dry-run results and rerun without --dry-run to apply the patch.";
        }

        if (result.Results.Any(item => item.WasPatched))
        {
            return "Re-run pefix inspect or pefix scan to confirm the updated directory state.";
        }

        return "No changes were needed.";
    }

    private static void WriteSection(StringWriter writer, string title, string[] entries)
    {
        if (entries.Length == 0)
        {
            return;
        }

        writer.WriteLine();
        writer.WriteLine($"  {title}:");
        foreach (string entry in entries)
        {
            writer.WriteLine($"    - {entry}");
        }
    }

    private static string FormatPath(string root, string path)
    {
        return Path.GetRelativePath(root, path);
    }
}
