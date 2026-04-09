using PeFix.Commands;
using System.CommandLine;

namespace PeFix;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Diagnose and fix managed assembly PE header portability issues.");
        rootCommand.Add(Inspect.Create());
        rootCommand.Add(Fix.Create());
        rootCommand.Add(Scan.Create());

        return rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());
    }
}
