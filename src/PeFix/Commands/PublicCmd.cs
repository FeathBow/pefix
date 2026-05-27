using System.CommandLine;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class PublicCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("publicize", "Publicize types/methods/fields in a managed assembly. Default dry-run; pass --apply to write.");
        cmd.Aliases.Add("publicise");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Public.Run(
            r.GetValue(opts.PathArg)!,
            new PubOptions(
                Backup: !r.GetValue(opts.NoBackupOpt),
                DryRun: !r.GetValue(opts.ApplyOpt)),
            r.GetValue(RootCmd.JsonOpt)));
        return cmd;
    }

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
