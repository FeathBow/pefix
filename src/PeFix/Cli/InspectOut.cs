using PeFix.Meta;

namespace PeFix.Cli;

internal static class InspectOut
{
    public static string Render(Inspection result)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"pefix {Path.GetFileName(result.Path)}");
        writer.WriteLine();
        writer.WriteLine($"  Status:  {Labels.StatusHead(result.Status)}");
        writer.WriteLine($"  Summary: {InspectText.Summary(result)}");
        writer.WriteLine($"  Action:  {InspectText.Action(result)}");
        writer.WriteLine();
        writer.WriteLine("  Details:");
        writer.WriteLine($"    PE Format:     {result.PeFormat ?? "Unknown"} ({result.Machine ?? "Unknown"})");
        writer.WriteLine($"    IL Only:       {FormatBool(result.CliFlags.IlOnly)}");
        writer.WriteLine($"    Strong Name:   {FormatBool(result.Signals.StrongName)}");
        writer.WriteLine($"    P/Invoke:      {FormatBool(result.Signals.HasPInvoke)}");
        writer.WriteLine($"    Category:      {Labels.CatText(result.Category)}");
        writer.WriteLine($"    Status:        {Labels.StatusText(result.Status)}");
        return writer.ToString().TrimEnd();
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }
}
