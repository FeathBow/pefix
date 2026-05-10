using PeFix.Patch;

namespace PeFix.Cli;

internal static class RedirOut
{
    public static string Render(RedirResult r)
    {
        string status = r.WasDryRun ? "DRY-RUN"
            : r.RowsPatched > 0 ? "PATCHED"
            : "UNCHANGED";

        string summary = r.WasDryRun && r.RowsPatched == 0 ? "No matching AssemblyRef rows found."
            : r.WasDryRun ? $"Would redirect {r.RowsPatched} AssemblyRef row(s)."
            : r.RowsPatched > 0 ? $"Redirected {r.RowsPatched} AssemblyRef row(s)."
            : "No matching AssemblyRef rows found; nothing to redirect.";

        string action = r.WasDryRun ? $"Run: pefix redir {Path.GetFileName(r.Path)} --from <name>:<ver> --to <ver> --apply"
            : r.RowsPatched > 0 ? BackupAction(r)
            : "No action needed.";

        List<(string, string)> details = new()
        {
            ("Rows Patched:", r.RowsPatched.ToString())
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
            "redir",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string BackupAction(RedirResult r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }
}
