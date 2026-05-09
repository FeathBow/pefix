using System.CommandLine;
using PeFix.Commands;

namespace PeFix;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PathCmd.WriteStart();
            return Task.FromResult(0);
        }

        Command command = PathCmd.Create();
        return command.Parse(args).InvokeAsync(new InvocationConfiguration());
    }
}
