using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class BepInExProviderIndex
{
    private readonly HashSet<string> _exact = new(StringComparer.Ordinal);
    private readonly HashSet<string> _folded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<BepInExPluginProvider>> _byGuid = new(StringComparer.Ordinal);

    private BepInExProviderIndex()
    {
    }

    public static BepInExProviderIndex Empty { get; } = new();

    public static BepInExProviderIndex From(Inspection[] results)
    {
        BepInExProviderIndex index = new();
        foreach (Inspection result in results)
        {
            if (!result.BepInEx.HasValue)
                continue;

            foreach (BepInExPluginMetadata plugin in result.BepInEx.Value.Plugins)
            {
                index._exact.Add(plugin.Guid);
                index._folded.Add(plugin.Guid);
                index.AddProvider(plugin, result.Path);
            }
        }

        return index;
    }

    public BepInExProviderMatch MatchFor(string dependencyGuid)
    {
        if (_exact.Contains(dependencyGuid))
            return BepInExProviderMatch.Exact;

        return _folded.Contains(dependencyGuid)
            ? BepInExProviderMatch.CaseOnly
            : BepInExProviderMatch.None;
    }

    public BepInExPluginProvider[] ForExactGuid(string guid)
    {
        return _byGuid.TryGetValue(guid, out List<BepInExPluginProvider>? providers)
            ? [.. providers]
            : [];
    }

    public BepInExPluginProvider[] ForAnyCase(string guid)
    {
        return [.. _byGuid
            .Where(item => string.Equals(item.Key, guid, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Value)];
    }

    public BepInExPluginProvider[] DuplicateProviders()
    {
        return [.. _byGuid
            .Where(item => item.Value.Count > 1)
            .SelectMany(item => item.Value)];
    }

    private void AddProvider(BepInExPluginMetadata plugin, string path)
    {
        if (!_byGuid.TryGetValue(plugin.Guid, out List<BepInExPluginProvider>? providers))
        {
            providers = [];
            _byGuid.Add(plugin.Guid, providers);
        }

        providers.Add(new BepInExPluginProvider(plugin.Guid, plugin.Version, path));
    }
}
