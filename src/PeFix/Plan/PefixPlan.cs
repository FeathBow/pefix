using System.Text.Json.Serialization;

namespace PeFix.Plan;

public sealed record PefixPlan(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("tool")] PlanTool Tool,
    [property: JsonPropertyName("inputs")] PlanFile[] Inputs,
    [property: JsonPropertyName("ops")] MutationOp[] Ops,
    [property: JsonPropertyName("outputs")] PlanFile[] Outputs,
    [property: JsonPropertyName("rollback")] PlanRollback Rollback,
    [property: JsonPropertyName("provenance")] PlanMeta Provenance);
