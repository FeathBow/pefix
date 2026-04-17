using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Scan
{
    internal static CliExit Run(string path, bool json, string? failOn, bool onConflict)
    {
        Status? threshold = null;
        if (failOn is not null)
        {
            if (!SevArg.TryParse(failOn, out Status value))
                return SevArg.WriteBad(failOn);

            threshold = value;
        }

        ScanReport report;
        try
        {
            report = Scanner.Scan(path);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        if (json)
        {
            JsonOut.Write(JsonWriter.Render(report));
        }
        else
        {
            Console.WriteLine(ScanWriter.Render(report));
        }

        if (onConflict && report.Conflicts.Length > 0)
            return CliExit.Issue;

        return threshold is { } t && report.Results.Any(r => r.Status >= t)
            ? CliExit.Issue
            : CliExit.Success;
    }
}
