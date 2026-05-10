using PeFix.Patch;

namespace PeFix.Cli;

internal static class PublicWriter
{
    public static string Render(PublicResult r)
    {
        string status = r.WasDryRun ? "DRY-RUN"
            : r.OpsCount > 0 ? "PATCHED"
            : "UNCHANGED";

        string summary = r.WasDryRun ? $"Would flip {r.OpsCount} visibility flag(s)."
            : r.OpsCount > 0 ? $"Flipped {r.OpsCount} visibility flag(s)."
            : "No non-public members found; nothing to publicize.";

        string action = r.WasDryRun ? $"Run: pefix publicize {Path.GetFileName(r.Path)} --apply"
            : r.OpsCount > 0 ? BackupAction(r)
            : "No action needed.";

        List<(string, string)> details = new()
        {
            ("Ops:", r.OpsCount.ToString())
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

    private static string BackupAction(PublicResult r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }
}
