namespace PeFix.Cli;

internal static class Baseline
{
    public static string[] Lines(DirectoryIssue[] issues)
    {
        return [.. issues
            .SelectMany(LineSet)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)];
    }

    public static string[] Parse(string[] rawLines)
    {
        return [.. rawLines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)];
    }

    public static BaselineDiff Diff(string[] current, string[] known)
    {
        HashSet<string> knownSet = new(known, StringComparer.Ordinal);
        HashSet<string> currentSet = new(current, StringComparer.Ordinal);
        string[] fresh = [.. current.Where(line => !knownSet.Contains(line))];
        string[] stale = [.. known
            .Where(line => !currentSet.Contains(line))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)];
        return new BaselineDiff(fresh, stale, current.Length - fresh.Length);
    }

    private static IEnumerable<string> LineSet(DirectoryIssue issue)
    {
        if (issue.Files.Length == 0)
        {
            yield return Line(issue, "-");
            yield break;
        }

        foreach (string file in issue.Files)
            yield return Line(issue, file);
    }

    private static string Line(DirectoryIssue issue, string file)
    {
        return $"{issue.Code}|{issue.Subject}|{file}";
    }
}
