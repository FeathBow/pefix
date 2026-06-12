namespace PeFix.Cli;

internal static class BaselineOut
{
    public static string Render(string path, BaselineDiff diff)
    {
        using var writer = new StringWriter();
        writer.WriteLine($"{Indent.Of(1)}Baseline: {path}");
        writer.WriteLine($"{Indent.Of(2)}matched: {diff.Matched}  new: {diff.Fresh.Length}  stale: {diff.Stale.Length}");
        foreach (string line in diff.Fresh)
            writer.WriteLine($"{Indent.Of(2)}- new: {line}");

        foreach (string line in diff.Stale)
            writer.WriteLine($"{Indent.Of(2)}- stale: {line}");

        return writer.ToString().TrimEnd();
    }

    public static string RenderWritten(string path, int count)
    {
        string word = count == 1 ? "entry" : "entries";
        return $"{Indent.Of(1)}Baseline: wrote {count} {word} to {path}";
    }
}
