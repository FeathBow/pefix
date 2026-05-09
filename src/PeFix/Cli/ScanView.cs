namespace PeFix.Cli;

internal sealed record ScanView(
    string Directory,
    ScanStats Stats,
    ScanFile[] Files,
    DirConf[] Conflicts,
    DirMiss[] MissingRefs,
    DirDup[] DupProviders,
    DirIssue[] Issues,
    ScanJsonMeta? Json = null)
{
    public bool HasIssues => Issues.Length > 0;
}
