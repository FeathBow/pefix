using PeFix.Meta;

namespace PeFix.Cli;

internal static class Labels
{
    public static string CatText(Category? category)
    {
        return category switch
        {
            Category.ManagedPePortability => "managed_pe_portability",
            Category.ReferenceAssemblyMisuse => "reference_assembly_misuse",
            Category.NonRewritableBinary => "non_rewritable_binary",
            _ => "unknown"
        };
    }

    public static string StatusHead(Status status)
    {
        return status switch
        {
            Status.Compatible => "COMPATIBLE",
            Status.Fixable => "FIXABLE",
            Status.FixableWithWarnings => "FIXABLE_WITH_WARNINGS",
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
            Status.FixableWithWarnings => "fixable-with-warnings",
            Status.Unsafe => "unsafe",
            Status.Corrupt => "corrupt",
            _ => "unknown"
        };
    }
}
