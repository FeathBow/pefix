using System.CommandLine;

namespace PeFix.Commands;

internal static class RedirCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("redir", "Rewrite AssemblyRef version fields. Defaults to dry-run; pass --apply to write.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Redir.Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(opts.FromOpt),
            r.GetValue(opts.ToOpt),
            !r.GetValue(opts.NoBackupOpt),
            !r.GetValue(opts.ApplyOpt),
            r.GetValue(RootCmd.JsonOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to redirect."
        };
        public Option<string?> FromOpt { get; } = new("--from")
        {
            Description = "Match AssemblyRef in form <name>:<version>."
        };
        public Option<string?> ToOpt { get; } = new("--to")
        {
            Description = "Target version (Major.Minor.Build.Revision)."
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
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(FromOpt);
            cmd.Options.Add(ToOpt);
            cmd.Options.Add(NoBackupOpt);
            cmd.Options.Add(ApplyOpt);
        }
    }
}
