namespace PeFix.Meta;

public static class Scanner
{
    public static ScanReport Scan(string path)
    {
        string fullPath = Path.GetFullPath(path);
        CheckDir(fullPath);
        string[] files = ScanFiles(fullPath).ToArray();
        var results = new Inspection[files.Length];
        Parallel.For(0, files.Length, index => results[index] = PeAnalyzer.Inspect(files[index]));
        VerConflict[] conflicts = FindConfs(results);
        return new ScanReport(fullPath, results, conflicts);
    }

    public static bool HasFixable(ScanReport report)
    {
        return report.Results.Any(IsFixable);
    }

    public static bool IsFixable(Inspection result)
    {
        return result.Status is Status.Fixable or Status.Cautioned;
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
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static VerConflict[] FindConfs(Inspection[] results)
    {
        // Build map: assembly name (lowered) -> (version, file path).
        // When multiple DLLs share the same AssemblyDef name (e.g. derived fixtures),
        // skip duplicates — ambiguous provider cannot be resolved.
        Dictionary<string, (string Version, string Path)> provided = [];
        foreach (Inspection r in results.Where(r => r.AssemblyDef.HasValue))
        {
            string key = r.AssemblyDef!.Value.Name.ToLowerInvariant();
            if (!provided.ContainsKey(key))
            {
                provided[key] = (r.AssemblyDef!.Value.Version, r.Path);
            }
        }

        List<VerConflict> conflicts = [];
        foreach (Inspection inspection in results)
        {
            foreach (AsmRef asmRef in inspection.AssemblyRefs ?? [])
            {
                string key = asmRef.Name.ToLowerInvariant();
                if (provided.TryGetValue(key, out (string Version, string Path) found)
                    && !string.Equals(found.Version, asmRef.Version, StringComparison.Ordinal))
                {
                    conflicts.Add(new VerConflict(
                        asmRef.Name,
                        asmRef.Version,
                        found.Version,
                        Path.GetFileName(inspection.Path),
                        Path.GetFileName(found.Path)));
                }
            }
        }
        return [.. conflicts.DistinctBy(c => (c.AssemblyName, c.ReferencedBy))];
    }
}
