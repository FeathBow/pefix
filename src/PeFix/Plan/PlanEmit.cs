using System.Text.Json;

namespace PeFix.Plan;

public static class PlanEmit
{
    public static string SidecarPath(string targetPath) =>
        targetPath + ".pefix-plan.json";

    public static void Write(string targetPath, PefixPlan plan)
    {
        string sidecar = SidecarPath(targetPath);
        string tmp = $"{sidecar}.tmp.{Environment.ProcessId}";
        File.WriteAllText(tmp, PlanJson.Write(plan));
        File.Move(tmp, sidecar, overwrite: true);
    }

    public static void Write(string targetPath, PlanFile input, PlanFile output, IReadOnlyList<MutationOp> ops, string? backupPath)
    {
        JsonElement rollbackData = backupPath is null
            ? JsonDocument.Parse("null").RootElement
            : JsonDocument.Parse(JsonSerializer.Serialize(backupPath, PeFix.JsonContext.Default.String)).RootElement;
        PefixPlan plan = new(
            Version: 1,
            Tool: new PlanTool("pefix", PefixVersion()),
            Inputs: [input],
            Ops: [.. ops],
            Outputs: [output],
            Rollback: new PlanRollback(backupPath is null ? "none" : "bak", rollbackData),
            Provenance: new PlanMeta(Sha: null, Host: null, Ts: DateTimeOffset.UtcNow));
        Write(targetPath, plan);
    }

    private static string PefixVersion() =>
        typeof(PlanEmit).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
