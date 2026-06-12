namespace PeFix.Meta;

public static class Scanner
{
    public static ScanReport Scan(string path, HostProfile? hostProfile = null)
    {
        DirectoryInspection dir = InspectDir(path);
        var dependencies = DependencyIndex.Build(dir.Results, hostProfile);
        return new ScanReport(dir.Directory, dir.Results, FindGaps(dir.Results, dependencies));
    }

    internal static GapSet FindGaps(IReadOnlyList<Inspection> results, DependencyIndex dependencies)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(dependencies);

        return new GapSet
        {
            Conflicts = dependencies.FindConflicts(results),
            MissingReferences = dependencies.FindMissingReferences(results),
            DuplicateProviders = DependencyIndex.FindDuplicateProviders(results),
            MemberRefGaps = MemberSurfaceAnalyzer.FindMethodGaps(results, dependencies),
            TypeRefGaps = MemberSurfaceAnalyzer.FindTypeGaps(results, dependencies),
            FieldRefGaps = MemberSurfaceAnalyzer.FindFieldGaps(results, dependencies),
            ImplGaps = ImplAnalyzer.FindImplGaps(results, dependencies),
            AccessGaps = AccessScan.FindAccessGaps(results, dependencies)
        };
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
