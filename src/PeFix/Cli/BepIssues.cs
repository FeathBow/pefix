using PeFix.Meta;

namespace PeFix.Cli;

internal static class BepIssues
{
    public static DirectoryIssue[] Build(
        Inspection[] results,
        ScanPathRelativizer rel,
        BepInExProviderIndex index)
    {
        return BepInExExplain.Explain(results, rel, index).Issues;
    }
}
