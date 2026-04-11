using PeFix.Cli;
using PeFix.Meta;
using System.CommandLine;

namespace PeFix.Commands;

internal static class Scan
{
    public static Command Create()
    {
        var pathArg = new Argument<string>("path") { Description = "Directory containing assemblies to inspect." };
        var jsonOpt = new Option<bool>("--json") { Description = "Output results as JSON." };
        var failOnFixableOpt = new Option<bool>("--fail-on-fixable") { Description = "Exit with code 1 if fixable assemblies are found." };

        var command = new Command("scan", "Scan a directory for portability issues.");
        command.Arguments.Add(pathArg);
        command.Options.Add(jsonOpt);
        command.Options.Add(failOnFixableOpt);

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg)!;
            var json = parseResult.GetValue(jsonOpt);
            var failOnFixable = parseResult.GetValue(failOnFixableOpt);

            var report = Scanner.Scan(path);
            Console.WriteLine(json ? JsonWriter.Render(report.Results) : ScanWriter.Render(report));
            return failOnFixable && Scanner.HasFixable(report) ? 1 : 0;
        });

        return command;
    }
}
