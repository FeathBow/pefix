namespace PeFix.Meta;

public static class Scanner
{
    public static ScanReport Scan(string path)
    {
        var fullPath = Path.GetFullPath(path);
        CheckDir(fullPath);
        var files = ScanFiles(fullPath).ToArray();
        var results = new Inspection[files.Length];
        Parallel.For(0, files.Length, index => results[index] = PeAnalyzer.Inspect(files[index]));
        return new ScanReport(fullPath, results);
    }

    public static bool HasFixable(ScanReport report)
    {
        return report.Results.Any(IsFixable);
    }

    public static bool IsFixable(Inspection result)
    {
        return result.Status is Status.Fixable or Status.FixableWithWarnings;
    }

    private static void CheckDir(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory was not found: {path}");
        }
    }

    private static string[] ScanFiles(string path)
    {
        return Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(IsCandidate)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsCandidate(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
