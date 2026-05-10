using System.Globalization;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class RedirOut
{
    public static string Render(RedirResult r)
    {
        string status = Status(r);
        string summary = SummaryOf(r);
        string action = ActionOf(r);

        List<(string, string)> details = new()
        {
            ("Rows Patched:", r.RowsPatched.ToString(CultureInfo.InvariantCulture))
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

    private static string Status(RedirResult r) => (r.WasDryRun, r.RowsPatched > 0) switch
    {
        (true, _) => "DRY-RUN",
        (false, true) => "PATCHED",
        _ => "UNCHANGED",
    };

    private static string SummaryOf(RedirResult r) => (r.WasDryRun, r.RowsPatched) switch
    {
        (true, 0) => "No matching AssemblyRef rows found.",
        (true, _) => $"Would redirect {r.RowsPatched} AssemblyRef row(s).",
        (false, > 0) => $"Redirected {r.RowsPatched} AssemblyRef row(s).",
        _ => "No matching AssemblyRef rows found; nothing to redirect.",
    };

    private static string ActionOf(RedirResult r) => (r.WasDryRun, r.RowsPatched > 0) switch
    {
        (true, _) => $"Run: pefix redir {Path.GetFileName(r.Path)} --from <name>:<ver> --to <ver> --apply",
        (false, true) => BackupAction(r),
        _ => "No action needed.",
    };

    private static string BackupAction(RedirResult r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }
}
