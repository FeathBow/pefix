namespace PeFix.Meta;

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
