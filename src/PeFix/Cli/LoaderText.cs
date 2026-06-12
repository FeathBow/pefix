using PeFix.Meta;

namespace PeFix.Cli;

internal static class LoaderText
{
    public static string? GenerationToken(LoaderGeneration generation) => generation switch
    {
        LoaderGeneration.BepInEx5 => "bepinex5",
        LoaderGeneration.BepInEx6 => "bepinex6",
        _ => null
    };

    public static string? FlavorToken(LoaderFlavor flavor) => flavor switch
    {
        LoaderFlavor.Mono => "mono",
        LoaderFlavor.Il2Cpp => "il2cpp",
        _ => null
    };
}
