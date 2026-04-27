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
}
