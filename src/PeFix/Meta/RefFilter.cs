namespace PeFix.Meta;

public static class RefFilter
{
    private static readonly HashSet<string> ExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib",
        "netstandard",
        "Microsoft.CSharp",
        "Microsoft.VisualBasic",
        "Microsoft.VisualBasic.Core",
        "0Harmony",
        "Harmony",
        "GodotSharp",
        "BepInEx",
        "MelonLoader",
    };

    private static readonly string[] PrefixNames =
    [
        "System.",
        "WindowsBase",
        "PresentationCore",
        "PresentationFramework",
        "UnityEngine.",
        "UnityEditor.",
        "BepInEx.",
        "MelonLoader.",
    ];

    public static bool IsProvided(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ExactNames.Contains(name)
            || PrefixNames.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
