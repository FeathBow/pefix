using System.CommandLine;

namespace PeFix.Commands;

internal static class Fix
{
    public static Command Create()
    {
        var command = new Command("fix", "Attempt a safe PE header portability fix.");
        command.Arguments.Add(new Argument<string>("path")
        {
            Description = "Managed assembly path."
        });
        command.SetAction(_ =>
        {
            Console.Error.WriteLine("fix is not implemented yet.");
            return 1;
        });
        return command;
    }
}
