namespace PeFix.Meta;

public static class Scanner
{
    public static ScanReport Scan(string path)
    {
        DirInspect dir = InspectDir(path);
        var deps = DepIndex.Build(dir.Results);
        VerConflict[] conflicts = deps.FindConflicts(dir.Results);
        MissingRef[] missingRefs = deps.FindMissing(dir.Results);
        DupProvider[] dupProviders = DepIndex.FindDuplicates(dir.Results);
        return new ScanReport(dir.Directory, dir.Results, conflicts, missingRefs, dupProviders);
    }

    public static DirInspect InspectDir(string path)
    {
        string fullPath = Path.GetFullPath(path);
        CheckDir(fullPath);
        string[] files = ScanFiles(fullPath);
        var results = new Inspection[files.Length];
        Parallel.For(0, files.Length, index => results[index] = PeAnalyzer.Inspect(files[index]));
        return new DirInspect(fullPath, results);
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
        string extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wasm", StringComparison.OrdinalIgnoreCase);
    }
}
