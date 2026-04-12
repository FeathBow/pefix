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
