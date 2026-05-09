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

        ScanView view = ScanBuild.Build(report, json);

        if (json)
        {
            JsonOut.Write(JsonWriter.Render(view));
        }
        else
        {
            Console.WriteLine(ScanWriter.Render(view));
        }

        if (onConflict && view.Stats.HasConflict)
            return CliExit.Issue;

        return threshold is { } t && view.Files.Any(file => GateEval.Meets(file.Status, t))
            ? CliExit.Issue
            : CliExit.Success;
    }
}
