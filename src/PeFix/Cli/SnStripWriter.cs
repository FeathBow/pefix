using PeFix.Patch;

namespace PeFix.Cli;

internal static class SnStripWriter
{
    public static string Render(SnStripRes r)
    {
        string status = r.WasDryRun ? "DRY-RUN"
            : r.WasPatched ? "PATCHED"
            : "UNCHANGED";

        string summary = r.WasDryRun ? "Would strip strong-name signing from this assembly."
            : r.WasPatched ? "Strong-name signing stripped."
            : "Assembly is not strong-name signed; nothing to strip.";

        string action = r.WasDryRun ? $"Run: pefix snstrip {Path.GetFileName(r.Path)} --apply"
            : r.WasPatched ? BackupAction(r)
            : "No action needed.";

        List<(string, string)> details = new()
        {
            ("Strong Name:", r.WasPatched ? "No (was Yes)" : r.WasDryRun ? "Yes" : "No"),
            ("Signed IVT:", FormatYesNo(r.HadSignedIvt))
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

        if (r.DepsPatched > 0)
        {
            details.Add(("Deps Patched:", r.DepsPatched.ToString()));
        }

        return new MutBlock(
            Path.GetFileName(r.Path),
            "snstrip",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string BackupAction(SnStripRes r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }

    private static string FormatYesNo(bool value) => value ? "Yes" : "No";
}
