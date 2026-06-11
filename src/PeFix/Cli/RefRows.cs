using PeFix.Meta;

namespace PeFix.Cli;

internal static class RefRows
{
    public static RefFinding[] Build(RefFinding[] finds, PathRelativizer rel)
    {
        ArgumentNullException.ThrowIfNull(finds);
        ArgumentNullException.ThrowIfNull(rel);

        return [.. finds
            .Where(IsRow)
            .Select(find => Rel(find, rel))];
    }

    public static RefFinding[] Of(RefFinding[] finds, RefOutcome kind)
    {
        return [.. finds
            .Where(find => find.Resolution == kind)
            .OrderBy(find => find.ReferenceName, StringComparer.Ordinal)];
    }

    public static int Count(RefFinding[] finds, RefOutcome kind)
    {
        return finds.Count(find => find.Resolution == kind);
    }

    private static bool IsRow(RefFinding find)
    {
        return find.Confidence == Confidence.Gate
            && find.Resolution is RefOutcome.VersionConflict
                or RefOutcome.Missing
                or RefOutcome.DuplicateProvider;
    }

    private static RefFinding Rel(RefFinding find, PathRelativizer rel)
    {
        return find with
        {
            ConsumerPath = RelPath(find.ConsumerPath, rel),
            ProviderPath = find.ProviderPath is null ? null : rel.RelativePath(find.ProviderPath),
            ProviderPaths = find.ProviderPaths is null ? null : rel.RelativePaths(find.ProviderPaths)
        };
    }

    private static string RelPath(string path, PathRelativizer rel)
    {
        return path.Length == 0 ? path : rel.RelativePath(path);
    }
}
