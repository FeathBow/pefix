using System.CommandLine;

namespace PeFix.Commands;

internal static class Scan
{
    public static Command Create()
    {
        var command = new Command("scan", "Scan a directory for portability issues.");
        command.Arguments.Add(new Argument<string>("path")
        {
            Description = "Directory containing assemblies to inspect."
        });
        command.SetAction(_ =>
        {
            Console.Error.WriteLine("scan is not implemented yet.");
            return 1;
        });
        return command;
    }
}
