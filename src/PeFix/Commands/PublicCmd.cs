using System.CommandLine;
using System.Text.Json;
using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class PublicCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("publicize", "Publicize types/methods/fields in a managed assembly. Default dry-run; pass --apply to write.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Run(
            r.GetValue(opts.PathArg)!,
            new PubOptions(
                Backup: !r.GetValue(opts.NoBackupOpt),
                DryRun: !r.GetValue(opts.ApplyOpt)),
            r.GetValue(PathCmd.JsonOpt)));
        return cmd;
    }

    private static CliExit Run(string path, PubOptions options, bool json)
    {
        return PathRun.FileOnly(path, file => PathRun.Try(() => RunFile(file, options, json)));
    }

    private static CliExit RunFile(string path, PubOptions options, bool json)
    {
        PublicResult result = PublicPatch.Publicize(path, options);
        if (json) JsonOut.Write(ToJson(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static void WriteText(PublicResult r)
    {
        Console.WriteLine(PublicWriter.Render(r));
    }

    private static string ToJson(PublicResult r) =>
        JsonSerializer.Serialize(
            new PublicJson(r.Path, r.BackupPath, r.PlanPath, r.WasDryRun, r.OpsCount),
            JsonContext.Default.PublicJson);

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file to publicize."
        };
        public Option<bool> ApplyOpt { get; } = new("--apply")
        {
            Description = "Write the file. Without this flag the run is dry-run only."
        };
        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation."
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(ApplyOpt);
            cmd.Options.Add(NoBackupOpt);
        }
    }
}
