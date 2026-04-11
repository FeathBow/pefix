using System.CommandLine;
using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Fix
{
    public static Command Create()
    {
        var pathArg = new Argument<string>("path") { Description = "Managed assembly or directory path." };
        var jsonOpt = new Option<bool>("--json") { Description = "Output results as JSON." };
        var dryRunOpt = new Option<bool>("--dry-run") { Description = "Report without modifying files." };
        var forceOpt = new Option<bool>("--force") { Description = "Allow patching assemblies with warnings." };
        var noBackupOpt = new Option<bool>("--no-backup") { Description = "Skip .bak file creation." };

        var command = new Command("fix", "Attempt a safe PE header portability fix.");
        command.Arguments.Add(pathArg);
        command.Options.Add(jsonOpt);
        command.Options.Add(dryRunOpt);
        command.Options.Add(forceOpt);
        command.Options.Add(noBackupOpt);

        command.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(pathArg)!;
            bool json = parseResult.GetValue(jsonOpt);
            var options = new PatchOptions(
                Backup: !parseResult.GetValue(noBackupOpt),
                DryRun: parseResult.GetValue(dryRunOpt),
                Force: parseResult.GetValue(forceOpt));

            return Directory.Exists(path)
                ? RunDirectory(path, options, json)
                : RunFile(path, options, json);
        });

        return command;
    }

    private static int RunFile(string path, PatchOptions options, bool json)
    {
        try
        {
            PatchResult result = Patcher.Fix(path, options);
            Console.WriteLine(json ? JsonWriter.Render(result) : FixWriter.Render(result));
            return result.WasPatched ? 2 : 0;
        }
        catch (UnsafeException ex)
        {
            if (json)
            {
                Inspection before = PeAnalyzer.Inspect(path);
                Console.WriteLine(JsonWriter.Render(new Refusal(path, ex.Message, before)));
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
            }

            return 3;
        }
    }

    private static int RunDirectory(string path, PatchOptions options, bool json)
    {
        BatchResult result = BatchPatcher.Fix(path, options);
        Console.WriteLine(json ? JsonWriter.Render(result) : BatchWriter.Render(result));

        if (result.Results.Any(r => r.WasPatched))
        {
            return 2;
        }

        return result.Refusals.Length > 0 ? 3 : 0;
    }
}
