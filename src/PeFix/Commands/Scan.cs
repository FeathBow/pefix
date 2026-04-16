using System.CommandLine;
using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Scan
{
    public static Command Create()
    {
        var pathArg = new Argument<string>("path") { Description = "Directory containing assemblies to inspect." };
        var jsonOpt = new Option<bool>("--json") { Description = "Output results as JSON." };
        var failOnOpt = new Option<string?>("--fail-on")
        {
            Description = "Exit with code 1 when any result meets or exceeds the given severity (compatible, fixable, cautioned, unsafe, corrupt)."
        };
        var conflictOpt = new Option<bool>("--fail-on-conflict")
        {
            Description = "Exit with code 1 when version conflicts are detected between assemblies."
        };

        var command = new Command("scan", "Scan a directory for portability issues.");
        command.Arguments.Add(pathArg);
        command.Options.Add(jsonOpt);
        command.Options.Add(failOnOpt);
        command.Options.Add(conflictOpt);

        command.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(pathArg)!;
            bool json = parseResult.GetValue(jsonOpt);
            string? failOn = parseResult.GetValue(failOnOpt);
            Status? threshold = null;
            bool onConflict = parseResult.GetValue(conflictOpt);

            if (failOn is not null)
            {
                if (!SevArg.TryParse(failOn, out Status value))
                    return SevArg.WriteBad(failOn);

                threshold = value;
            }

            ScanReport report = Scanner.Scan(path);
            if (json)
            {
                JsonOut.Write(JsonWriter.Render(report));
            }
            else
            {
                Console.WriteLine(ScanWriter.Render(report));
            }

            if (onConflict && report.Conflicts.Length > 0)
                return 1;

            return threshold is { } t && report.Results.Any(r => r.Status >= t) ? 1 : 0;
        });

        return command;
    }
}
