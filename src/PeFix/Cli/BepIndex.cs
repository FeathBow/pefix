using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class BepIndex
{
    private readonly HashSet<string> _exact = new(StringComparer.Ordinal);
    private readonly HashSet<string> _folded = new(StringComparer.OrdinalIgnoreCase);

    private BepIndex()
    {
    }

    public static BepIndex From(Inspection[] results)
    {
        BepIndex index = new();
        foreach (BepInfo? bep in results.Select(result => result.Bep))
        {
            if (!bep.HasValue)
                continue;

            foreach (string guid in bep.Value.Plugins.Select(plugin => plugin.Guid))
            {
                index._exact.Add(guid);
                index._folded.Add(guid);
            }
        }

        return index;
    }

    public BepDepState Status(string guid)
    {
        if (_exact.Contains(guid))
            return BepDepState.Present;

        return _folded.Contains(guid)
            ? BepDepState.CaseMismatch
            : BepDepState.Missing;
    }
}
