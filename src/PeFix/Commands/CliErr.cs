namespace PeFix.Commands;

internal static class CliErr
{
    public static CliExit Usage(string message)
    {
        Console.Error.WriteLine(message);
        return CliExit.Usage;
    }

    public static CliExit Io(string message)
    {
        Console.Error.WriteLine(message);
        return CliExit.Io;
    }

    public static CliExit Io(Exception ex)
    {
        return Io(ex.Message);
    }
}
