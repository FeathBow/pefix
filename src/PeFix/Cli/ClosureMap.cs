using PeFix.Meta;

namespace PeFix.Cli;

internal static class ClosureMap
{
    public static ClosureJson Map(ClosureReport report)
    {
        return new ClosureJson(
            report.Directory,
            report.Entries,
            [.. report.Unresolved.Select(MapChain)],
            [.. report.CycleChains.Select(MapChain)],
            report.RefsWalked,
            report.ProvidedLeaves.Total,
            report.ProvidedLeaves.Framework);
    }

    private static ChainJson MapChain(ClosureChain chain)
    {
        return new ChainJson(
            chain.Entry.AssemblyName,
            [.. chain.Segments.Select(seg => new SegmentJson(
                seg.AssemblyName,
                seg.Version,
                KindLabel(seg.Kind)))]);
    }

    private static string KindLabel(ChainKind kind) => kind switch
    {
        ChainKind.Entry => "entry",
        ChainKind.Resolved => "resolved",
        ChainKind.Unresolved => "unresolved",
        ChainKind.Cycle => "cycle",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
