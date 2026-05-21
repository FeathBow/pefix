using System.Text.Json;

namespace PeFix.Plan;

public static class PlanEmit
{
    public static string SidecarPath(string targetPath) =>
        targetPath + ".pefix-plan.json";

    internal static PefixPlan Create(Request request)
    {
        JsonElement rollbackData = request.BackupPath is null
            ? JsonDocument.Parse("null").RootElement
            : JsonDocument.Parse(JsonSerializer.Serialize(request.BackupPath, PeFix.JsonContext.Default.String)).RootElement;
        return new PefixPlan(
            Version: 1,
            Tool: new PlanTool("pefix", PefixVersion()),
            Inputs: [request.Input],
            Ops: [.. request.Ops],
            Outputs: [request.Output],
            Rollback: new PlanRollback(request.BackupPath is null ? "none" : "bak", rollbackData),
            Provenance: new PlanMeta(Sha: null, Host: null, Ts: DateTimeOffset.UtcNow));
    }

    internal static string Stage(string targetPath, PefixPlan plan)
    {
        string sidecar = SidecarPath(targetPath);
        if (Directory.Exists(sidecar))
            throw new IOException($"Plan path {sidecar} is a directory.");

        string temporaryPath = $"{sidecar}.tmp.{Environment.ProcessId}";
        try
        {
            File.WriteAllText(temporaryPath, PlanJson.Write(plan));
            return temporaryPath;
        }
        catch
        {
            DeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    internal static void Commit(string targetPath, string temporaryPath)
    {
        File.Move(temporaryPath, SidecarPath(targetPath), overwrite: true);
    }

    internal static void DeleteTemporaryFile(string? temporaryPath)
    {
        if (temporaryPath is not null && File.Exists(temporaryPath))
            File.Delete(temporaryPath);
    }

    private static string PefixVersion() =>
        typeof(PlanEmit).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    internal sealed class Request
    {
        public required PlanFile Input { get; init; }
        public required PlanFile Output { get; init; }
        public required IReadOnlyList<MutationOp> Ops { get; init; }
        public required string? BackupPath { get; init; }
    }
}
