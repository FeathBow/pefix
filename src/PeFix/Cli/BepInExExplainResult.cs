using System.Collections.ObjectModel;

namespace PeFix.Cli;

internal sealed record BepInExExplainResult(
    DirectoryIssue[] Issues,
    ReadOnlyDictionary<string, string> FileStates)
{
    public string? StateForFile(string path)
    {
        return FileStates.GetValueOrDefault(path);
    }
}
