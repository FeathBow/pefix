using System.Globalization;
using PeFix.Patch;

namespace PeFix.Cli;

internal static class SnStripOut
{
    public static string Render(SnStripResult r)
    {
        string status = Status(r);
        string summary = SummaryOf(r);
        string action = ActionOf(r);

        List<(string, string)> details = new()
        {
            ("Strong Name:", StrongNameLabel(r)),
            ("Signed IVT:", InspectOut.FormatBool(r.HadSignedIvt)),
            ("Targets:", TargetText.Format(r.Ops)),
            ("Repair Class:", RepairClassLabel(r)),
            ("Not Proven:", SnStripJson.UnverifiedRiskText)
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
            string depLabel = r.WasDryRun ? "Dependencies:" : "Dependencies Patched:";
            details.Add((depLabel, r.DepsPatched.ToString(CultureInfo.InvariantCulture)));
            details.Add(("Dependency Targets:", FormatDependencyTargets(r)));
        }

        return new MutBlock(
            Path.GetFileName(r.Path),
            "snstrip",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string Status(SnStripResult r) => r.Outcome switch
    {
        SnStripOutcome.DryRun => "DRY-RUN",
        SnStripOutcome.Patched => "PATCHED",
        _ => "UNCHANGED",
    };

    private static string SummaryOf(SnStripResult r) => r.Outcome switch
    {
        SnStripOutcome.DryRun => "Would strip strong-name signing from this assembly.",
        SnStripOutcome.Patched => "Strong-name signing stripped.",
        SnStripOutcome.DepRefused => "Dependency rewrite was refused; assembly was left unchanged.",
        _ => "Assembly is not strong-name signed; nothing to strip.",
    };

    private static string ActionOf(SnStripResult r) => r.Outcome switch
    {
        SnStripOutcome.DryRun => $"Run: pefix snstrip {Path.GetFileName(r.Path)} --apply",
        SnStripOutcome.Patched => BackupAction(r),
        SnStripOutcome.DepRefused => "Resolve dependency refusal and rerun snstrip.",
        _ => "No action needed.",
    };

    private static string StrongNameLabel(SnStripResult r) => r.Outcome switch
    {
        SnStripOutcome.Patched => "No (was Yes)",
        SnStripOutcome.DryRun => r.WasSigned ? "Yes" : "No",
        SnStripOutcome.DepRefused => "Yes",
        _ => "No",
    };

    private static string RepairClassLabel(SnStripResult r) => r.Outcome == SnStripOutcome.Unsigned
        ? RepairClass.DiagnosticOnly
        : SnStripJson.RepairClassValue;

    private static string BackupAction(SnStripResult r)
    {
        return r.BackupPath is not null
            ? $"Backup written to {Path.GetFileName(r.BackupPath)}. Run pefix scan <dir> --json to re-check the folder."
            : "Backup skipped (--no-backup). Run pefix scan <dir> --json to re-check the folder.";
    }

    private static string FormatDependencyTargets(SnStripResult result)
    {
        return string.Join("; ", result.Deps.Select(FormatDependency));
    }

    private static string FormatDependency(SnDependency dependency)
    {
        return $"{Path.GetFileName(dependency.Path)}: {TargetText.Format(dependency.Ops)}";
    }
}
