namespace PeFix.Cli;

internal sealed class ScanPathRelativizer
{
    private readonly string _root;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScanPathRelativizer(string root)
    {
        _root = root;
    }

    public string RelativePath(string path)
    {
        if (_cache.TryGetValue(path, out string? relativePath))
            return relativePath;

        relativePath = Path.GetRelativePath(_root, path).Replace('\\', '/');
        _cache[path] = relativePath;
        return relativePath;
    }

    public string[] RelativePaths(string[] paths)
    {
        string[] relativePaths = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            relativePaths[i] = RelativePath(paths[i]);
        return relativePaths;
    }
}
