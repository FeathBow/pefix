using PeFix.Meta;

namespace PeFix.Cli;

internal static class Labels
{
    public static string CatText(Category? category)
    {
        return category switch
        {
            Category.Portability => "portability",
            Category.RefAssembly => "ref_assembly",
            Category.NativeBinary => "native_binary",
            Category.MixedMode => "mixed_mode",
            Category.PlatformApi => "platform_api",
            Category.R2R => "r2r_compat",
            Category.Trimmable => "trimmable",
            Category.ModuleNest => "module_nest",
            Category.MultiModule => "multi_module",
            Category.Satellite => "satellite",
            Category.Bundle => "bundle",
            Category.Webcil => "webcil",
            _ => "unknown"
        };
    }

    public static string StatusHead(Status status)
    {
        return status switch
        {
            Status.Compatible => "COMPATIBLE",
            Status.Fixable => "FIXABLE",
            Status.Cautioned => "CAUTIONED",
            Status.Unsafe => "UNSAFE",
            Status.Corrupt => "CORRUPT",
            _ => "UNKNOWN"
        };
    }

    public static string StatusText(Status status)
    {
        return status switch
        {
            Status.Compatible => "compatible",
            Status.Fixable => "fixable",
            Status.Cautioned => "cautioned",
            Status.Unsafe => "unsafe",
            Status.Corrupt => "corrupt",
            _ => "unknown"
        };
    }
}
