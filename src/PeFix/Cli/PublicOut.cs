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
            ("Ops:", r.OpsCount.ToString(CultureInfo.InvariantCulture)),
            ("Targets:", TargetText.Format(r.Ops)),
            ("Repair Class:", PublicJson.RepairClassValue),
            ("Not Proven:", PublicJson.UnverifiedRiskText)
        };

        MutOut.AddWriteDetails(details, r.WasDryRun, r.Path, r.BackupPath, r.PlanPath);
        return new MutBlock(
            Path.GetFileName(r.Path),
            "publicize",
            status,
            summary,
            action,
            details.ToArray()).Render();
    }

    private static string Status(PublicResult r) => MutOut.RunStatus(r.WasDryRun, r.OpsCount > 0);

    private static string SummaryOf(PublicResult r) => (r.WasDryRun, r.OpsCount > 0) switch
    {
        (true, _) => $"Would flip {r.OpsCount} visibility flag(s).",
        (false, true) => $"Flipped {r.OpsCount} visibility flag(s).",
        _ => "No non-public members found; nothing to publicize.",
    };

    private static string ActionOf(PublicResult r) => (r.WasDryRun, r.OpsCount > 0) switch
    {
        (true, _) => $"Run: pefix publicize {Path.GetFileName(r.Path)} --apply",
        (false, true) => MutOut.BackupAction(r.BackupPath),
        _ => "No action needed.",
    };
}
