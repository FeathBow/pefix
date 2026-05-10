using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class FixOut
{
    public static string Render(PatchResult result)
    {
        string status = result.DryRun ? "DRY-RUN"
            : result.WasPatched ? "PATCHED"
            : "UNCHANGED";

        string summary = result.DryRun ? "Would patch PE header to AnyCPU."
            : result.WasPatched ? "PE header patched to AnyCPU."
            : "Assembly already compatible; nothing to fix.";

        string action = result.DryRun ? $"Run: pefix fix {Path.GetFileName(result.Path)} --apply"
            : result.WasPatched ? BackupAction(result)
            : "No action needed.";

        List<(string, string)> details = new()
        {
            ("PE Format:", FormatPe(result)),
            ("Status Before:", InspectCompat(result.Before.Status)),
            ("Status After:", result.DryRun ? "not written" : InspectCompat(result.After.Status)),
            ("Backup:", result.DryRun ? "(not created)" : BackupDetail(result)),
            ("Verify:", Verify(result))
        };

        return new MutBlock(
            Path.GetFileName(result.Path),
            "fix",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string FormatPe(PatchResult result)
    {
        return $"{result.Before.PeFormat ?? "Unknown"} ({result.Before.Machine ?? "Unknown"})";
    }

    private static string InspectCompat(Status status)
    {
        return status == Status.Compatible
            ? "compatible"
            : "not compatible";
    }

    private static string BackupDetail(PatchResult result)
    {
        return result.BackupPath is not null
            ? Path.GetFileName(result.BackupPath)
            : "(not created)";
    }

    private static string BackupAction(PatchResult result)
    {
        return result.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(result.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }

    private static string Verify(PatchResult result)
    {
        if (result.DryRun)
            return "skipped (--dry-run)";
        if (!result.WasPatched)
            return "skipped (already compatible)";
        return "re-inspection passed";
    }
}
