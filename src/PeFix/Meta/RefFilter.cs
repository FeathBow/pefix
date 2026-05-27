namespace PeFix.Meta;

public sealed class HostProfile
{
    public const string DefaultName = "default";
    public const string UnityBepInExName = "unity-bepinex";

    public static HostProfile Default { get; } = new(
        DefaultName,
        ProvidedLeafRules.DefaultExactNames,
        ProvidedLeafRules.DefaultPrefixNames);

    public static HostProfile UnityBepInEx { get; } = new(
        UnityBepInExName,
        ProvidedLeafRules.DefaultExactNames,
        ProvidedLeafRules.DefaultPrefixNames);

    private readonly IReadOnlyDictionary<string, ProvidedKind> _exactNames;
    private readonly IReadOnlyList<ProvidedLeafPrefix> _prefixNames;

    internal HostProfile(
        string name,
        IReadOnlyDictionary<string, ProvidedKind> exactNames,
        IReadOnlyList<ProvidedLeafPrefix> prefixNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _exactNames = exactNames;
        _prefixNames = prefixNames;
    }

    public string Name { get; }

    internal ProvidedKind Classify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_exactNames.TryGetValue(name, out ProvidedKind exact))
            return exact;

        foreach (ProvidedLeafPrefix prefix in _prefixNames)
        {
            if (name.StartsWith(prefix.Prefix, StringComparison.OrdinalIgnoreCase))
                return prefix.Kind;
        }

        return ProvidedKind.None;
    }
}

public static class RefFilter
{
    public static bool IsProvided(string name)
    {
        return IsProvided(name, HostProfile.Default);
    }

    public static bool IsProvided(string name, HostProfile hostProfile)
    {
        return Classify(name, hostProfile) != ProvidedKind.None;
    }

    internal static ProvidedKind Classify(string name)
    {
        return Classify(name, HostProfile.Default);
    }

    internal static ProvidedKind Classify(string name, HostProfile hostProfile)
    {
        ArgumentNullException.ThrowIfNull(hostProfile);
        return hostProfile.Classify(name);
    }
}

internal readonly record struct ProvidedLeafPrefix(string Prefix, ProvidedKind Kind);

internal static class ProvidedLeafRules
{
    internal static readonly Dictionary<string, ProvidedKind> DefaultExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mscorlib"] = ProvidedKind.Framework,
        ["System"] = ProvidedKind.Framework,
        ["netstandard"] = ProvidedKind.Framework,
        ["Microsoft.CSharp"] = ProvidedKind.Framework,
        ["Microsoft.VisualBasic"] = ProvidedKind.Framework,
        ["Microsoft.VisualBasic.Core"] = ProvidedKind.Framework,
        ["0Harmony"] = ProvidedKind.Loader,
        ["Harmony"] = ProvidedKind.Loader,
        ["GodotSharp"] = ProvidedKind.Host,
        ["UnityEngine"] = ProvidedKind.Host,
        ["BepInEx"] = ProvidedKind.Loader,
        ["MelonLoader"] = ProvidedKind.Loader,
    };

    internal static readonly ProvidedLeafPrefix[] DefaultPrefixNames =
    [
        new("System.", ProvidedKind.Framework),
        new("WindowsBase", ProvidedKind.Framework),
        new("PresentationCore", ProvidedKind.Framework),
        new("PresentationFramework", ProvidedKind.Framework),
        new("UnityEngine.", ProvidedKind.Host),
        new("UnityEditor.", ProvidedKind.Host),
        new("BepInEx.", ProvidedKind.Loader),
        new("MelonLoader.", ProvidedKind.Loader),
    ];
}
