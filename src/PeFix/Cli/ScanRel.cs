namespace PeFix.Cli;

internal sealed class ScanRel
{
    private readonly string _root;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScanRel(string root)
    {
        _root = root;
    }

    public string One(string path)
    {
        if (_cache.TryGetValue(path, out string? rel))
            return rel;

        rel = Path.GetRelativePath(_root, path);
        _cache[path] = rel;
        return rel;
    }

    public string[] Many(string[] paths)
    {
        string[] rels = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            rels[i] = One(paths[i]);
        return rels;
    }
}
