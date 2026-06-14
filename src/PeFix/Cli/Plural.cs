namespace PeFix.Cli;

internal static class Plural
{
    public static string Count(int n, string singular, string? plural = null) =>
        n == 1 ? $"1 {singular}" : $"{n} {plural ?? singular + "s"}";
}
