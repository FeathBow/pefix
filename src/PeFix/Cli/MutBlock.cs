namespace PeFix.Cli;

internal readonly record struct MutBlock(
    string FileName,
    string Verb,
    string Status,
    string Summary,
    string Action,
    (string key, string value)[] Details)
{
    public string Render()
    {
        using StringWriter writer = new();
        writer.WriteLine($"pefix {FileName}{(Verb.Length > 0 ? " " + Verb : "")}");
        writer.WriteLine();
        writer.WriteLine($"  Status:  {Status}");
        writer.WriteLine($"  Summary: {Summary}");
        writer.WriteLine($"  Action:  {Action}");
        writer.WriteLine();
        writer.WriteLine("  Details:");
        int padLen = Details.Length > 0
            ? Details.Max(d => d.key.Length) + 4
            : 0;
        foreach ((string key, string value) in Details)
        {
            writer.WriteLine($"    {key.PadRight(padLen)}{value}");
        }
        return writer.ToString().TrimEnd();
    }
}
