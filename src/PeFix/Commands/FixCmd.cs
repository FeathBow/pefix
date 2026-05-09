using System.CommandLine;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class FixCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("fix",
            "Apply the safe PE header fix. Defaults to dry-run; pass --apply to write.");
        opts.AddTo(cmd);
        cmd.SetAction(r =>
        {
            string path = r.GetValue(opts.PathArg)!;
            bool apply = r.GetValue(opts.ApplyOpt);
            bool json = r.GetValue(opts.JsonOpt);
            bool noBackup = r.GetValue(opts.NoBackupOpt);
            bool force = r.GetValue(opts.ForceOpt);
            PatchOptions options = new(
                Backup: !noBackup,
                DryRun: !apply,
                Force: force);
            return (int)Fix.Run(path, options, json);
        });
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to fix."
        };

        public Option<bool> ApplyOpt { get; } = new("--apply")
        {
            Description = "Write changes to disk. Without this flag, the command only reports what would change.",
            DefaultValueFactory = _ => false
        };

        public Option<bool> JsonOpt { get; } = new("--json")
        {
            Description = "Write structured JSON output."
        };

        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation. Only meaningful with --apply."
        };

        public Option<bool> ForceOpt { get; } = new("--force")
        {
            Description = "Allow patching cautioned assemblies. Only meaningful with --apply."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(ApplyOpt);
            cmd.Options.Add(JsonOpt);
            cmd.Options.Add(NoBackupOpt);
            cmd.Options.Add(ForceOpt);
        }
    }
}
