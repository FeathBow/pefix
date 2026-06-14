using System.Text.Json;

namespace PeFix.Meta;

// Reads the runtime-asset names declared by *.deps.json. A referenced assembly absent
// from this set is shared-framework provided, not missing; null when no manifest exists.
internal static class DepsReader
{
    public static IReadOnlySet<string>? ReadDeclaredAssets(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        string[] files = Directory
            .EnumerateFiles(directory, "*.deps.json", SearchOption.AllDirectories)
            .ToArray();
        if (files.Length == 0)
            return null;

        HashSet<string> assets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            CollectAssets(file, assets);

        // All-unreadable falls back to the name-based rules, never suppressing every ref.
        return assets.Count > 0 ? assets : null;
    }

    private static void CollectAssets(string file, HashSet<string> assets)
    {
        DepsJson? manifest = Parse(file);
        if (manifest?.Targets is null)
            return;

        IEnumerable<string> runtimeAssets = manifest.Targets.Values
            .SelectMany(target => target.Values)
            .Where(library => library.Runtime is not null)
            .SelectMany(library => library.Runtime!.Keys);

        foreach (string assetPath in runtimeAssets)
            assets.Add(AssemblyName(assetPath));
    }

    private static DepsJson? Parse(string file)
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), JsonContext.Default.DepsJson);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string AssemblyName(string assetPath)
    {
        ReadOnlySpan<char> leaf = Path.GetFileName(assetPath.AsSpan());
        return leaf.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? leaf[..^4].ToString()
            : leaf.ToString();
    }
}
