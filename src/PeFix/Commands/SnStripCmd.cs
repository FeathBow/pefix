using System.CommandLine;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class SnStripCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("snstrip", "Strip strong-name signing. Defaults to dry-run; pass --apply to write.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)SnStrip.Run(
            r.GetValue(opts.PathArg)!,
            new SnStripOpts(
                Backup: !r.GetValue(opts.NoBackupOpt),
                DryRun: !r.GetValue(opts.ApplyOpt),
                Force: r.GetValue(opts.ForceOpt)),
            r.GetValue(RootCmd.JsonOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to strip."
        };
        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation."
        };
        public Option<bool> ApplyOpt { get; } = new("--apply")
        {
            Description = "Write changes to disk. Without this flag, the command only reports what would change.",
            DefaultValueFactory = _ => false
        };
        public Option<bool> ForceOpt { get; } = new("--force")
        {
            Description = "Strip even when InternalsVisibleTo uses a signed PublicKey."
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(NoBackupOpt);
            cmd.Options.Add(ApplyOpt);
            cmd.Options.Add(ForceOpt);
        }
    }
}
