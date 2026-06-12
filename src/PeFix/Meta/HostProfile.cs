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

        ProvidedKind kind = _prefixNames
            .Where(prefix => name.StartsWith(prefix.Prefix, StringComparison.OrdinalIgnoreCase))
            .Select(prefix => prefix.Kind)
            .FirstOrDefault(ProvidedKind.None);
        return kind;
    }
}
