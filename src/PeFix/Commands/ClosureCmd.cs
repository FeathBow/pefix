using System.CommandLine;

namespace PeFix.Commands;

internal static class ClosureCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("closure", "Walks the transitive AssemblyRef graph and reports unresolved dependency chains.");
        opts.AddTo(cmd);
        cmd.SetAction(r =>
        {
            string path = r.GetValue(opts.PathArg)!;
            bool json = r.GetValue(RootCmd.JsonOpt);
            bool fail = r.GetValue(opts.MissingOpt);
            return (int)(r.GetValue(opts.TreeOpt)
                ? Closure.RunTree(path, json, fail)
                : Closure.Run(path, json, fail));
        });
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

        public Option<bool> TreeOpt { get; } = new("--tree")
        {
            Description = "Include the full transitive dependency tree."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(MissingOpt);
            cmd.Options.Add(TreeOpt);
        }
    }
}
