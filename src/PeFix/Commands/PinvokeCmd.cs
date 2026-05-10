using System.CommandLine;

namespace PeFix.Commands;

internal static class PinvokeCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("pinvoke", "List P/Invoke calls in a managed assembly or directory.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Pinvoke.Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(RootCmd.JsonOpt)));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to inspect."
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
        }
    }
}
