using System.CommandLine;

namespace PeFix.Commands;

internal static class Inspect
{
    public static Command Create()
    {
        var command = new Command("inspect", "Inspect a managed assembly for portability issues.");
        command.Arguments.Add(new Argument<string>("path")
        {
            Description = "Managed assembly path."
        });
        command.SetAction(_ =>
        {
            Console.Error.WriteLine("inspect is not implemented yet.");
            return 1;
        });
        return command;
    }
}
