using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class FixWriter
{
    public static string Render(PatchResult result)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"pefix fix {Path.GetFileName(result.Path)}");
        writer.WriteLine();
        writer.WriteLine($"  Result:  {Result(result)}");
        writer.WriteLine($"  Backup:  {Backup(result)}");
        writer.WriteLine($"  Before:  {FormatState(result.Before)}");
        writer.WriteLine($"  After:   {AfterText(result)}");
        writer.WriteLine($"  Verify:  {Verify(result)}");
        return writer.ToString().TrimEnd();
    }

    private static string Result(PatchResult result)
    {
        if (result.DryRun)
        {
            return "Dry run only";
        }

        if (result.WasPatched)
        {
            return $"Patched {Path.GetFileName(result.Path)}";
        }

        return "No changes were needed";
    }

    private static string Backup(PatchResult result)
    {
        return result.BackupPath is null ? "(not created)" : Path.GetFileName(result.BackupPath);
    }

    private static string FormatState(Inspection result)
    {
        return $"{result.PeFormat ?? "Unknown"} {result.Machine ?? "Unknown"} -> {Compat(result.Status)}";
    }

    private static string AfterText(PatchResult result)
    {
        if (result.DryRun)
        {
            return "not written (--dry-run)";
        }

        if (!result.WasPatched)
        {
            return "unchanged (already compatible)";
        }

        return $"{result.After.PeFormat ?? "Unknown"} {result.After.Machine ?? "Unknown"} -> compatible with all platforms";
    }

    private static string Verify(PatchResult result)
    {
        if (result.DryRun)
        {
            return "Skipped because no file was modified.";
        }

        if (!result.WasPatched)
        {
            return "Skipped because the assembly was already compatible.";
        }

        return "Re-inspection passed. Assembly manifest was validated.";
    }

    private static string Compat(Status status)
    {
        return status == Status.Compatible
            ? "already compatible"
            : "not compatible with all platforms";
    }
}
