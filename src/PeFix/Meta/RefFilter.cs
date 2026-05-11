namespace PeFix.Meta;

public static class RefFilter
{
    private static readonly Dictionary<string, ProvidedKind> ExactNames = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly (string Prefix, ProvidedKind Kind)[] PrefixNames =
    [
        ("System.", ProvidedKind.Framework),
        ("WindowsBase", ProvidedKind.Framework),
        ("PresentationCore", ProvidedKind.Framework),
        ("PresentationFramework", ProvidedKind.Framework),
        ("UnityEngine.", ProvidedKind.Host),
        ("UnityEditor.", ProvidedKind.Host),
        ("BepInEx.", ProvidedKind.Loader),
        ("MelonLoader.", ProvidedKind.Loader),
    ];

    public static bool IsProvided(string name)
    {
        return Classify(name) != ProvidedKind.None;
    }

    internal static ProvidedKind Classify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ExactNames.TryGetValue(name, out ProvidedKind exact))
            return exact;

        foreach ((string prefix, ProvidedKind kind) in PrefixNames)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        return ProvidedKind.None;
    }
}
