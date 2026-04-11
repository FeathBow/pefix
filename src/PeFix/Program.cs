using System.CommandLine;
using PeFix.Commands;

namespace PeFix;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Diagnose and fix managed assembly PE header portability issues.")
        {
            Inspect.Create(),
            Fix.Create(),
            Scan.Create()
        };

        return rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());
    }
}
