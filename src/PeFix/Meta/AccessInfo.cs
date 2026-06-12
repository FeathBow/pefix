namespace PeFix.Meta;

internal sealed record AccessInfo(
    HashSet<string> IvtNames,
    HashSet<string> SkipNames)
{
    public static AccessInfo Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
