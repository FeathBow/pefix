using PeFix.Cli;
using PeFix.Meta;
using System.CommandLine;

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
        var failOnFixableOption = new Option<bool>("--fail-on-fixable")
        {
            Description = "Return exit code 1 when fixable assemblies are found."
        };

        command.Arguments.Add(pathArgument);
        command.Options.Add(jsonOption);
        command.Options.Add(failOnFixableOption);
        command.SetAction(parseResult => Execute(
            parseResult.GetValue(pathArgument),
            parseResult.GetValue(jsonOption),
            parseResult.GetValue(failOnFixableOption)));

        return command;
    }

    private static int Execute(string? path, bool asJson, bool failOnFixable)
    {
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
        return GetExitCode(result, failOnFixable);
    }

    private static int GetExitCode(Inspection result, bool failOnFixable)
    {
        if (failOnFixable)
        {
            return result.Status is Status.Fixable or Status.FixableWithWarnings ? 1 : 0;
        }

        return result.Status == Status.Compatible ? 0 : 1;
    }
}
