using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class DotNetHostProfileTests
{
    [Fact]
    public void DotNetHostProvidesFrameworkButNotUnityOrBepInEx()
    {
        Assert.True(RefFilter.IsProvided("System.Runtime", HostProfile.DotNet));
        Assert.True(RefFilter.IsProvided("mscorlib", HostProfile.DotNet));
        Assert.False(RefFilter.IsProvided("UnityEngine.CoreModule", HostProfile.DotNet));
        Assert.False(RefFilter.IsProvided("BepInEx", HostProfile.DotNet));
        Assert.False(RefFilter.IsProvided("0Harmony", HostProfile.DotNet));
    }

    [Fact]
    public void UnityBepInExHostStillProvidesUnityAndBepInEx()
    {
        Assert.True(RefFilter.IsProvided("UnityEngine.CoreModule", HostProfile.UnityBepInEx));
        Assert.True(RefFilter.IsProvided("BepInEx", HostProfile.UnityBepInEx));
        Assert.True(RefFilter.IsProvided("0Harmony", HostProfile.UnityBepInEx));
        Assert.False(RefFilter.IsProvided("GodotSharp", HostProfile.UnityBepInEx));
        Assert.False(RefFilter.IsProvided("MelonLoader", HostProfile.UnityBepInEx));
    }

    [Fact]
    public void DotNetPluginProfileParsesToDotNetHost()
    {
        Assert.True(ProfileParser.TryParse("dotnet-plugin", out ScanProfile? profile));
        Assert.Equal("dotnet", profile!.Host.Name);
        Assert.Equal("plugin-folder", profile.Artifact);
    }

    [Fact]
    public void PublishDirProfileParsesToPublishArtifact()
    {
        Assert.True(ProfileParser.TryParse("publish-dir", out ScanProfile? profile));
        Assert.Equal("dotnet", profile!.Host.Name);
        Assert.Equal("publish-dir", profile.Artifact);
    }
}
