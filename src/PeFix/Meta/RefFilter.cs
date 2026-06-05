namespace PeFix.Meta;

public sealed class HostProfile
{
    public const string DefaultName = "default";
    public const string UnityBepInExName = "unity-bepinex";

    public const string DotNetName = "dotnet";

    public static HostProfile Default { get; } = new(
        DefaultName,
        ProvidedLeafRules.DefaultExactNames,
        ProvidedLeafRules.DefaultPrefixNames);

    public static HostProfile UnityBepInEx { get; } = new(
        UnityBepInExName,
        ProvidedLeafRules.UnityBepInExExactNames,
        ProvidedLeafRules.UnityBepInExPrefixNames);

    // A generic .NET plugin host / publish directory: only the framework is
    // provided. Unity, BepInEx, Harmony and other loader assemblies are NOT
    // host-provided here, so referencing them without supplying them is a gap.
    public static HostProfile DotNet { get; } = new(
        DotNetName,
        ProvidedLeafRules.FrameworkExactNames,
        ProvidedLeafRules.FrameworkPrefixNames);

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
    internal static readonly Dictionary<string, ProvidedKind> FrameworkExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mscorlib"] = ProvidedKind.Framework,
        ["System"] = ProvidedKind.Framework,
        ["netstandard"] = ProvidedKind.Framework,
        ["Microsoft.CSharp"] = ProvidedKind.Framework,
        ["Microsoft.VisualBasic"] = ProvidedKind.Framework,
        ["Microsoft.VisualBasic.Core"] = ProvidedKind.Framework,
    };

    internal static readonly ProvidedLeafPrefix[] FrameworkPrefixNames =
    [
        new("System.", ProvidedKind.Framework),
        new("WindowsBase", ProvidedKind.Framework),
        new("PresentationCore", ProvidedKind.Framework),
        new("PresentationFramework", ProvidedKind.Framework),
    ];

    private static readonly Dictionary<string, ProvidedKind> LoaderHostExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0Harmony"] = ProvidedKind.Loader,
        ["Harmony"] = ProvidedKind.Loader,
        ["GodotSharp"] = ProvidedKind.Host,
        ["UnityEngine"] = ProvidedKind.Host,
        ["BepInEx"] = ProvidedKind.Loader,
        ["MelonLoader"] = ProvidedKind.Loader,
    };

    private static readonly ProvidedLeafPrefix[] LoaderHostPrefixNames =
    [
        new("UnityEngine.", ProvidedKind.Host),
        new("UnityEditor.", ProvidedKind.Host),
        new("BepInEx.", ProvidedKind.Loader),
        new("MelonLoader.", ProvidedKind.Loader),
    ];

    private static readonly Dictionary<string, ProvidedKind> UnityBepInExHostExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0Harmony"] = ProvidedKind.Loader,
        ["Harmony"] = ProvidedKind.Loader,
        ["UnityEngine"] = ProvidedKind.Host,
        ["BepInEx"] = ProvidedKind.Loader,
    };

    private static readonly ProvidedLeafPrefix[] UnityBepInExHostPrefixNames =
    [
        new("UnityEngine.", ProvidedKind.Host),
        new("UnityEditor.", ProvidedKind.Host),
        new("BepInEx.", ProvidedKind.Loader),
    ];

    internal static readonly Dictionary<string, ProvidedKind> UnityBepInExExactNames = Merge(
        FrameworkExactNames,
        UnityBepInExHostExactNames);

    internal static readonly ProvidedLeafPrefix[] UnityBepInExPrefixNames =
        [.. FrameworkPrefixNames, .. UnityBepInExHostPrefixNames];

    internal static readonly Dictionary<string, ProvidedKind> DefaultExactNames = Merge(FrameworkExactNames, LoaderHostExactNames);

    internal static readonly ProvidedLeafPrefix[] DefaultPrefixNames = [.. FrameworkPrefixNames, .. LoaderHostPrefixNames];

    private static Dictionary<string, ProvidedKind> Merge(
        Dictionary<string, ProvidedKind> first,
        Dictionary<string, ProvidedKind> second)
    {
        Dictionary<string, ProvidedKind> merged = new(first, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ProvidedKind> entry in second)
            merged[entry.Key] = entry.Value;

        return merged;
    }
}
