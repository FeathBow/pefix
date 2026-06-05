using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanProfile(
    HostProfile Host,
    string Artifact,
    LoaderTarget? DeclaredLoaderTarget = null);

internal static class ProfileParser
{
    public const string UnityBepInEx = HostProfile.UnityBepInExName;
    public const string UnityBepInEx5 = "unity-bepinex5";
    public const string UnityBepInEx6Mono = "unity-bepinex6-mono";
    public const string UnityBepInEx6Il2Cpp = "unity-bepinex6-il2cpp";
    public const string DotNetPlugin = "dotnet-plugin";
    public const string PublishDir = "publish-dir";
    public const string PluginFolder = "plugin-folder";

    public static bool TryParse(string? value, out ScanProfile? profile)
    {
        profile = value switch
        {
            null => null,
            UnityBepInEx => Unity(null),
            UnityBepInEx5 => Unity(new LoaderTarget(LoaderGeneration.BepInEx5, LoaderFlavor.Mono)),
            UnityBepInEx6Mono => Unity(new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Mono)),
            UnityBepInEx6Il2Cpp => Unity(new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Il2Cpp)),
            DotNetPlugin => new ScanProfile(HostProfile.DotNet, PluginFolder),
            PublishDir => new ScanProfile(HostProfile.DotNet, PublishDir),
            _ => null
        };
        return value is null || profile is not null;
    }

    private static ScanProfile Unity(LoaderTarget? declaredHost)
    {
        return new ScanProfile(HostProfile.UnityBepInEx, PluginFolder, declaredHost);
    }
}
