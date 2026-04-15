using System.CommandLine;
using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Inspect
{
    public static Command Create()
    {
        var command = new Command("inspect", "Inspect a managed assembly for portability issues.");
        var pathArgument = new Argument<string>("path")
        {
            Description = "Managed assembly to inspect."
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write structured JSON output."
        };
        var failOnOpt = new Option<string?>("--fail-on")
        {
            Description = "Exit with code 1 when the result meets or exceeds the given severity (compatible, fixable, cautioned, unsafe, corrupt)."
        };

        command.Arguments.Add(pathArgument);
        command.Options.Add(jsonOption);
        command.Options.Add(failOnOpt);
        command.SetAction(parseResult => Execute(
            parseResult.GetValue(pathArgument),
            parseResult.GetValue(jsonOption),
            parseResult.GetValue(failOnOpt)));

        return command;
    }

    private static int Execute(string? path, bool asJson, string? failOn)
    {
        Status? threshold = null;
        if (failOn is not null)
        {
            if (!SevArg.TryParse(failOn, out Status value))
                return SevArg.WriteBad(failOn);

            threshold = value;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("A file or directory path is required.");
            return 4;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine("A readable file path is required.");
            return 4;
        }

        Inspection result;
        try
        {
            result = PeAnalyzer.Inspect(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }

        Console.Out.WriteLine(asJson ? JsonWriter.Render(result) : InspectOut.Render(result));
        return GetExitCode(result, threshold);
    }

    private static int GetExitCode(Inspection result, Status? threshold)
    {
        if (threshold is { } t)
            return result.Status >= t ? 1 : 0;
        return result.Status == Status.Compatible ? 0 : 1;
    }
}
