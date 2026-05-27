namespace PeFix.Meta;

public static class Scanner
{
    public static ScanReport Scan(string path, HostProfile? hostProfile = null)
    {
        DirectoryInspection dir = InspectDir(path);
        var dependencies = DependencyIndex.Build(dir.Results, hostProfile);
        VersionConflict[] conflicts = dependencies.FindConflicts(dir.Results);
        MissingReference[] missingReferences = dependencies.FindMissingReferences(dir.Results);
        DuplicateProvider[] duplicateProviders = DependencyIndex.FindDuplicateProviders(dir.Results);
        return new ScanReport(dir.Directory, dir.Results, conflicts, missingReferences, duplicateProviders);
    }

    public static DirectoryInspection InspectDir(string path)
    {
        string fullPath = Path.GetFullPath(path);
        CheckDir(fullPath);
        string[] files = ScanFiles(fullPath);
        var results = new Inspection[files.Length];
        Parallel.For(0, files.Length, index => results[index] = PeAnalyzer.Inspect(files[index]));
        return new DirectoryInspection(fullPath, results);
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
