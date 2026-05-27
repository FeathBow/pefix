namespace PeFix.Cli;

internal static class BepVersionRange
{
    private const string MinimumPrefix = ">=";

    public static bool IsMinimumSatisfied(string? range, string version)
    {
        if (range is null)
            return true;

        if (!range.StartsWith(MinimumPrefix, StringComparison.Ordinal))
            return true;

        string minimum = range[MinimumPrefix.Length..].Trim();
        return Version.TryParse(version, out Version? actual)
            && Version.TryParse(minimum, out Version? required)
            && actual >= required;
    }
}
