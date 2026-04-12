using PeFix.Meta;

namespace PeFix.Cli;

internal static class InspectOut
{
    public static string Render(Inspection result)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"pefix inspect {Path.GetFileName(result.Path)}");
        writer.WriteLine();
        writer.WriteLine($"  Status:  {Labels.StatusHead(result.Status)}");
        writer.WriteLine($"  Summary: {GetSummary(result)}");
        writer.WriteLine($"  Action:  {Action(result)}");
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

    private static string Action(Inspection result)
    {
        return result.NextSteps.Length > 0 ? result.NextSteps[0] : "No action available.";
    }

    private static string GetSummary(Inspection result)
    {
        return result.Status switch
        {
            Status.Compatible => "This assembly is already portable across platforms.",
            Status.Fixable => "This assembly uses a platform-specific header, but the managed code is portable and can be fixed.",
            Status.Cautioned when result.Signals.StrongName => "This assembly can be fixed, but the strong name signature will be invalidated.",
            Status.Cautioned => "This assembly can be fixed, but native dependencies may still fail on the target platform.",
            Status.Unsafe => result.PrimaryCause,
            Status.Corrupt => result.PrimaryCause,
            _ => "The compatibility status could not be determined."
        };
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }
}
