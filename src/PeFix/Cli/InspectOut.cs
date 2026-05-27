using PeFix.Meta;

namespace PeFix.Cli;

internal static class InspectOut
{
    public static string Render(Inspection result)
    {
        return new MutBlock(
            FileName: Path.GetFileName(result.Path),
            Verb: "",
            Status: Labels.StatusHead(result.Status),
            Summary: InspectText.Summary(result),
            Action: InspectText.Action(result),
            Details: new (string, string)[]
            {
                ("PE Format:", $"{result.PeFormat ?? "Unknown"} ({result.Machine ?? "Unknown"})"),
                ("IL Only:", FormatBool(result.ManagedCorFlags.IlOnly)),
                ("Strong Name:", FormatBool(result.Signals.StrongName)),
                ("P/Invoke:", FormatBool(result.Signals.HasPInvoke)),
                ("Category:", Labels.CatText(result.Category)),
                ("Status:", Labels.StatusText(result.Status)),
            }
        ).Render();
    }

    internal static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }
}
