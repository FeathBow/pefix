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
        MissingRef[] missingRefs = FindMissing(results);
        DupProvider[] dupProviders = FindDup(results);
        return new ScanReport(fullPath, results, conflicts, missingRefs, dupProviders);
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
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wasm", StringComparison.OrdinalIgnoreCase);
    }

    private static VerConflict[] FindConfs(Inspection[] results)
    {
        Dictionary<string, (string Version, string Path)> provided = [];
        foreach (Inspection r in results.Where(r => r.AssemblyDef.HasValue))
        {
            string key = r.AssemblyDef!.Value.Name.ToLowerInvariant();
            provided.TryAdd(key, (r.AssemblyDef!.Value.Version, r.Path));
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

    private static MissingRef[] FindMissing(Inspection[] results)
    {
        HashSet<string> provided = FindProvided(results);
        List<MissingRef> missing = [];
        foreach (Inspection inspection in results)
        {
            foreach (AsmRef asmRef in inspection.AssemblyRefs ?? [])
            {
                if (provided.Contains(asmRef.Name) || RefFilter.IsProvided(asmRef.Name))
                    continue;

                missing.Add(new MissingRef(
                    asmRef.Name,
                    asmRef.Version,
                    Path.GetFileName(inspection.Path)));
            }
        }
        return [.. missing.DistinctBy(item => (item.RefName, item.NeedBy))];
    }

    private static DupProvider[] FindDup(Inspection[] results)
    {
        Dictionary<string, List<string>> found = new(StringComparer.OrdinalIgnoreCase);
        foreach (Inspection item in results.Where(item => item.AssemblyDef.HasValue))
        {
            string asmName = item.AssemblyDef!.Value.Name;
            if (!found.TryGetValue(asmName, out List<string>? files))
            {
                files = [];
                found[asmName] = files;
            }

            files.Add(Path.GetFileName(item.Path));
        }

        return [.. found
            .Where(item => item.Value.Count > 1)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new DupProvider(
                item.Key,
                [.. item.Value.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)]))];
    }

    private static HashSet<string> FindProvided(Inspection[] results)
    {
        return results
            .Where(item => item.AssemblyDef.HasValue)
            .Select(item => item.AssemblyDef!.Value.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

}
