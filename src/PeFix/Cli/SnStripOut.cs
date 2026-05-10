using System.Globalization;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class SnStripOut
{
    public static string Render(SnStripRes r)
    {
        string status = Status(r);
        string summary = SummaryOf(r);
        string action = ActionOf(r);

        List<(string, string)> details = new()
        {
            ("Strong Name:", StrongNameLabel(r)),
            ("Signed IVT:", InspectOut.FormatBool(r.HadSignedIvt))
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
            details.Add(("Deps Patched:", r.DepsPatched.ToString(CultureInfo.InvariantCulture)));
        }

        return new MutBlock(
            Path.GetFileName(r.Path),
            "snstrip",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string Status(SnStripRes r) => (r.WasDryRun, r.WasPatched) switch
    {
        (true, _) => "DRY-RUN",
        (false, true) => "PATCHED",
        _ => "UNCHANGED",
    };

    private static string SummaryOf(SnStripRes r) => (r.WasDryRun, r.WasPatched) switch
    {
        (true, _) => "Would strip strong-name signing from this assembly.",
        (false, true) => "Strong-name signing stripped.",
        _ => "Assembly is not strong-name signed; nothing to strip.",
    };

    private static string ActionOf(SnStripRes r) => (r.WasDryRun, r.WasPatched) switch
    {
        (true, _) => $"Run: pefix snstrip {Path.GetFileName(r.Path)} --apply",
        (false, true) => BackupAction(r),
        _ => "No action needed.",
    };

    private static string StrongNameLabel(SnStripRes r) => (r.WasPatched, r.WasDryRun) switch
    {
        (true, _) => "No (was Yes)",
        (false, true) => "Yes",
        _ => "No",
    };

    private static string BackupAction(SnStripRes r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}."
            : "Backup skipped (--no-backup).";
    }
}
