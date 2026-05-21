using System.CommandLine;

namespace PeFix.Commands;

internal static class ClosureCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("closure", "Walks the transitive AssemblyRef graph and reports unresolved dependency chains.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Closure.Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(RootCmd.JsonOpt),
            r.GetValue(opts.MissingOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Directory to walk closure for."
        };

        public Option<bool> MissingOpt { get; } = new("--fail-on-unresolved")
        {
            Description = "Exit with code 1 when any transitive dependency is unresolved."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(MissingOpt);
        }
    }
}
