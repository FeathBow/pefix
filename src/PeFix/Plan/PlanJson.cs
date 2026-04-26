using System.Text.Json;

namespace PeFix.Plan;

public static class PlanJson
{
    public static string Write(PefixPlan plan)
    {
        return JsonSerializer.Serialize(plan, JsonContext.Default.PefixPlan);
    }

    public static PefixPlan Read(string json)
    {
        return JsonSerializer.Deserialize(json, JsonContext.Default.PefixPlan)
            ?? throw new JsonException("PefixPlan JSON was empty.");
    }
}
