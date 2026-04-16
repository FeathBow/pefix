namespace PeFix.Cli;

internal static class JsonOut
{
    public static void Write(string json)
    {
        Console.Out.Write(json);
        Console.Out.Write('\n');
    }
}
