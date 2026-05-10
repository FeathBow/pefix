using System.CommandLine;
using PeFix.Commands;

namespace PeFix;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        Command command = RootCmd.Create();
        return command.Parse(args).InvokeAsync(new InvocationConfiguration());
    }
}
