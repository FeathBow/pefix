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

        var command = new Command("scan", "Scan a directory for portability issues.");
        command.Arguments.Add(pathArg);
        command.Options.Add(jsonOpt);
        command.Options.Add(failOnOpt);

        command.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(pathArg)!;
            bool json = parseResult.GetValue(jsonOpt);
            string? failOn = parseResult.GetValue(failOnOpt);
            Status? threshold = null;

            if (failOn is not null)
            {
                if (!SevArg.TryParse(failOn, out Status value))
                    return SevArg.WriteBad(failOn);

                threshold = value;
            }

            ScanReport report = Scanner.Scan(path);
            Console.WriteLine(json ? JsonWriter.Render(report) : ScanWriter.Render(report));
            return threshold is { } t && report.Results.Any(r => r.Status >= t) ? 1 : 0;
        });

        return command;
    }
}
