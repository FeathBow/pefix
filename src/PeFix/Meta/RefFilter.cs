namespace PeFix.Meta;

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
