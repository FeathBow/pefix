using System.Collections.ObjectModel;

namespace PeFix.Cli;

internal sealed record BepInExExplainResult(
    DirectoryIssue[] Issues,
    ReadOnlyDictionary<string, string> FileStates)
{
    public static BepInExExplainResult Empty { get; } = new(
        [],
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)));

    public string? StateForFile(string path)
    {
        return FileStates.GetValueOrDefault(path);
    }
}
