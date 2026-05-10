using System.CommandLine;

namespace PeFix.Commands;

internal static class InspectCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("inspect", "Inspects a single managed assembly.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Inspect.Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(RootCmd.JsonOpt),
            r.GetValue(opts.FailOnOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file to inspect."
        };

        public Option<string?> FailOnOpt { get; } = new("--fail-on")
        {
            Description = "Exit with code 1 when the result meets or exceeds the given severity."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(FailOnOpt);
        }
    }
}
