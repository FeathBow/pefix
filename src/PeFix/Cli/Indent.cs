namespace PeFix.Cli;

internal static class Indent
{
    private const int Width = 2;

    public static string Of(int level)
    {
        return new string(' ', level * Width);
    }
}
