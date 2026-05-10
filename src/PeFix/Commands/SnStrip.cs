using System.Text.Json;
using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class SnStrip
{
    internal static CliExit Run(string path, SnStripOpts options, bool json)
    {
        return PathRun.FileOrDir(
            path,
            file => PathRun.Try(() => RunFile(file, options, json)),
            dir => PathRun.Try(() => RunDir(dir, options, json)));
    }

    private static CliExit RunFile(string path, SnStripOpts options, bool json)
    {
        try
        {
            SnStripRes result = SnStripper.Strip(path, options);
            if (json)
                JsonOut.Write(ToJson(result));
            else
                WriteText(result);
            return result.DepFails.Length > 0 ? CliExit.Issue : CliExit.Success;
        }
        catch (UnsafeException ex)
        {
            if (json)
                JsonOut.Write(JsonSerializer.Serialize(
                    MapRefusal(new Refusal(Path.GetFullPath(path), ex.Message, PeAnalyzer.Inspect(path))),
                    JsonContext.Default.RefusalJson));
            else Console.Error.WriteLine(ex.Message);
            return CliExit.Issue;
        }
    }

    private static CliExit RunDir(string dir, SnStripOpts options, bool json)
    {
        SnBatch batch = SnStripper.StripDir(dir, options);
        if (json)
            JsonOut.Write(JsonSerializer.Serialize(
                new SnBatchJson(
                    batch.Directory,
                    batch.Results.Select(ToJsonRecord).ToArray(),
                    batch.Refusals.Select(MapRefusal).ToArray(),
                    batch.Deps.Select(ToDepJson).ToArray()),
                JsonContext.Default.SnBatchJson));
        else
        {
            foreach (SnStripRes r in batch.Results) WriteText(r);
            foreach (Refusal r in batch.Refusals) Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
            foreach (SnDep d in batch.Deps) Console.WriteLine($"dep: {d.Path}");
        }

        return batch.Refusals.Length > 0
            ? CliExit.Issue
            : CliExit.Success;
    }

    private static void WriteText(SnStripRes r)
    {
        Console.WriteLine(SnStripOut.Render(r));
        if (r.HadSignedIvt)
            Console.Error.WriteLine("warning: InternalsVisibleTo uses a signed PublicKey");
        foreach (Refusal fail in r.DepFails)
            Console.Error.WriteLine($"refused: {fail.Path}: {fail.Reason}");
    }

    private static string ToJson(SnStripRes r) =>
        JsonSerializer.Serialize(ToJsonRecord(r), JsonContext.Default.SnStripJson);

    private static SnStripJson ToJsonRecord(SnStripRes r) =>
        new(
            r.Path,
            r.BackupPath,
            r.PlanPath,
            r.WasPatched,
            r.WasDryRun,
            r.HadSignedIvt,
            r.DepsPatched,
            r.Deps.Select(ToDepJson).ToArray(),
            r.DepFails.Select(MapRefusal).ToArray());

    private static SnDepJson ToDepJson(SnDep dep) =>
        new(dep.Path, dep.BackupPath, dep.PlanPath);

    private static RefusalJson MapRefusal(Refusal refusal) =>
        new(refusal.Path, refusal.Reason, InspectMap.Map(refusal.Before));
}
