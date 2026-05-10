using System.Globalization;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class PublicOut
{
    public static string Render(PublicResult r)
    {
        string status = Status(r);
        string summary = SummaryOf(r);
        string action = ActionOf(r);

        List<(string, string)> details = new()
        {
            ("Ops:", r.OpsCount.ToString(CultureInfo.InvariantCulture))
        };

        if (r.WasDryRun)
        {
            details.Add(("Backup:", "Would write " + Path.GetFileName(r.Path) + ".bak"));
        }
        else if (r.BackupPath is not null)
        {
            details.Add(("Backup:", r.BackupPath));
        }

        if (!r.WasDryRun && r.PlanPath is not null)
        {
            details.Add(("Plan:", r.PlanPath));
        }

        return new MutBlock(
            Path.GetFileName(r.Path),
            "publicize",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string Status(PublicResult r) => (r.WasDryRun, r.OpsCount > 0) switch
    {
        (true, _) => "DRY-RUN",
        (false, true) => "PATCHED",
        _ => "UNCHANGED",
    };

    private static string SummaryOf(PublicResult r) => (r.WasDryRun, r.OpsCount > 0) switch
    {
        (true, _) => $"Would flip {r.OpsCount} visibility flag(s).",
        (false, true) => $"Flipped {r.OpsCount} visibility flag(s).",
        _ => "No non-public members found; nothing to publicize.",
    };

    private static string ActionOf(PublicResult r) => (r.WasDryRun, r.OpsCount > 0) switch
    {
        (true, _) => $"Run: pefix publicize {Path.GetFileName(r.Path)} --apply",
        (false, true) => BackupAction(r),
        _ => "No action needed.",
    };

    private static string BackupAction(PublicResult r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }
}
