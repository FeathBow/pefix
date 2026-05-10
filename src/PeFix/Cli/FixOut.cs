using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class FixOut
{
    public static string Render(PatchResult result)
    {
        string status = StatusOf(result);
        string summary = SummaryOf(result);
        string action = ActionOf(result);

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

    private static string StatusOf(PatchResult r) => (r.DryRun, r.WasPatched) switch
    {
        (true, _) => "DRY-RUN",
        (false, true) => "PATCHED",
        _ => "UNCHANGED",
    };

    private static string SummaryOf(PatchResult r) => (r.DryRun, r.WasPatched) switch
    {
        (true, _) => "Would patch PE header to AnyCPU.",
        (false, true) => "PE header patched to AnyCPU.",
        _ => "Assembly already compatible; nothing to fix.",
    };

    private static string ActionOf(PatchResult r) => (r.DryRun, r.WasPatched) switch
    {
        (true, _) => $"Run: pefix fix {Path.GetFileName(r.Path)} --apply",
        (false, true) => BackupAction(r),
        _ => "No action needed.",
    };

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
