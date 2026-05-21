using PeFix.Plan;

namespace PeFix.Cli;

internal static class TargetText
{
    public static string Format(IReadOnlyList<MutationOp> ops)
    {
        if (ops.Count == 0)
            return "none";

        return string.Join(", ", ops.Select(FormatOp));
    }

    private static string FormatOp(MutationOp op)
    {
        PlanTarget target = op.Target;
        string location = target.Table is not null && target.Row is not null
            ? $"{target.Table} row {target.Row}"
            : target.Kind;

        return target.Offset is null
            ? location
            : $"{location} @0x{target.Offset.Value:X}";
    }
}
