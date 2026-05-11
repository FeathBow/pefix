using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Closure
{
    internal static CliExit Run(string path, bool json, bool failOnMissing)
    {
        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
            return CliErr.Usage($"Path must be a directory: {fullPath}");

        ScanReport report;
        try
        {
            report = Scanner.Scan(fullPath);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        ClosureReport closure = ClosureGraph.Build(report.Results, fullPath);

        if (json)
        {
            var jsonObj = new ClosureJson(
                closure.Directory,
                closure.Entries,
                closure.Unresolved.Select(MapChain).ToArray(),
                closure.CycleChains.Select(MapChain).ToArray(),
                closure.RefsWalked,
                closure.HostLeaves);
            JsonOut.Write(System.Text.Json.JsonSerializer.Serialize(
                jsonObj, JsonContext.Default.ClosureJson));
        }
        else
        {
            Console.WriteLine(ClosureOut.Render(closure));
        }

        if (failOnMissing && closure.Unresolved.Length > 0)
            return CliExit.Issue;

        return CliExit.Success;
    }

    private static ChainJson MapChain(ClosureChain chain)
    {
        return new ChainJson(
            chain.Entry.AssemblyName,
            chain.Segments
                .Select(seg => new SegmentJson(seg.AssemblyName, seg.Version, KindLabel(seg.Kind)))
                .ToArray());
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
