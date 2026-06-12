namespace PeFix.Cli;

internal static class BaselineOut
{
    public static string Render(string path, BaselineDiff diff)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"  Baseline: {path}");
        writer.WriteLine($"    matched: {diff.Matched}  new: {diff.Fresh.Length}  stale: {diff.Stale.Length}");
        foreach (string line in diff.Fresh)
            writer.WriteLine($"    - new: {line}");

        foreach (string line in diff.Stale)
            writer.WriteLine($"    - stale: {line}");

        return writer.ToString().TrimEnd();
    }

    public static string RenderWritten(string path, int count)
    {
        string word = count == 1 ? "entry" : "entries";
        return $"  Baseline: wrote {count} {word} to {path}";
    }
}
