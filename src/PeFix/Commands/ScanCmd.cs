using System.CommandLine;

namespace PeFix.Commands;

internal static class ScanCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("scan", "Scans a directory of managed assemblies for portability and integrity issues.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Scan.Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(PathCmd.JsonOpt),
            r.GetValue(opts.FailOnOpt),
            r.GetValue(opts.ConflictOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Directory to scan."
        };

        public Option<string?> FailOnOpt { get; } = new("--fail-on")
        {
            Description = "Exit with code 1 when the result meets or exceeds the given severity."
        };

        public Option<bool> ConflictOpt { get; } = new("--fail-on-conflict")
        {
            Description = "Exit with code 1 when version conflicts are detected."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(FailOnOpt);
            cmd.Options.Add(ConflictOpt);
        }
    }
}
