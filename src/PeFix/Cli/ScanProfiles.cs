using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ArtifactProfile(string Name);

internal sealed record ScanProfiles(HostProfile HostProfile, ArtifactProfile ArtifactProfile)
{
    public string Host => HostProfile.Name;
    public string Artifact => ArtifactProfile.Name;
}

internal static class ScanProfile
{
    public const string UnityBepInEx = HostProfile.UnityBepInExName;
    public const string PluginFolder = "plugin-folder";

    private static readonly ArtifactProfile PluginFolderProfile = new(PluginFolder);

    public static bool TryParse(string? value, out ScanProfiles? profiles)
    {
        profiles = value switch
        {
            null => null,
            UnityBepInEx => new ScanProfiles(HostProfile.UnityBepInEx, PluginFolderProfile),
            _ => null
        };
        return value is null || profiles is not null;
    }
}
