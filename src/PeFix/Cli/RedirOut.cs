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
            ("Rows Patched:", r.RowsPatched.ToString(CultureInfo.InvariantCulture)),
            ("Targets:", TargetText.Format(r.Ops)),
            ("Repair Class:", RedirJson.RepairClassValue),
            ("Not Proven:", RedirJson.UnverifiedRiskText)
        };

        MutOut.AddWriteDetails(details, r.WasDryRun, r.Path, r.BackupPath, r.PlanPath);
        return new MutBlock(
            Path.GetFileName(r.Path),
            "redir",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string Status(RedirResult r) => MutOut.RunStatus(r.WasDryRun, r.RowsPatched > 0);

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
        (false, true) => MutOut.BackupAction(r.BackupPath),
        _ => "No action needed.",
    };
}
