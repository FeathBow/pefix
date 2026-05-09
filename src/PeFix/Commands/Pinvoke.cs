using System.CommandLine;
using System.Text.Json;
using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Pinvoke
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("pinvoke", "List P/Invoke calls in a managed assembly or directory.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(PathCmd.JsonOpt)));
        return cmd;
    }

    private static CliExit Run(string path, bool json)
    {
        return PathRun.FileOrDir(
            path,
            file => PathRun.Try(() => RunFile(file, json)),
            dir => PathRun.Try(() => RunDir(dir, json)));
    }

    private static CliExit RunFile(string path, bool json)
    {
        PinvokeRes result = PinvokeScan.Inspect(path);
        if (json) JsonOut.Write(ToJson(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static CliExit RunDir(string dir, bool json)
    {
        PinBatch batch = PinvokeScan.InspectDir(dir);
        if (json)
            JsonOut.Write(ToJson(batch));
        else
            WriteBatch(batch);
        return batch.Refusals.Length > 0 ? CliExit.Issue : CliExit.Success;
    }

    private static void WriteText(PinvokeRes r)
    {
        Console.WriteLine(r.Path);
        if (r.Calls.Length == 0)
        {
            Console.WriteLine("  (no P/Invoke calls)");
            return;
        }
        foreach (IGrouping<string, PinvokeCall> group in r.Calls.GroupBy(c => c.Module, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  module: {group.Key}");
            foreach (PinvokeCall c in group.OrderBy(c => c.DeclType, StringComparer.Ordinal).ThenBy(c => c.MethodName, StringComparer.Ordinal))
            {
                Console.WriteLine($"    {c.DeclType}.{c.MethodName} -> {c.EntryName}");
            }
        }
    }

    private static string ToJson(PinvokeRes r) =>
        JsonSerializer.Serialize(ToJsonRecord(r), JsonContext.Default.PinvokeJson);

    private static string ToJson(PinBatch batch) =>
        JsonSerializer.Serialize(
            new PinBatchJson(
                batch.Directory,
                batch.Results.Select(ToJsonRecord).ToArray(),
                batch.Refusals.Select(MapRefusal).ToArray()),
            JsonContext.Default.PinBatchJson);

    private static PinvokeJson ToJsonRecord(PinvokeRes r) =>
        new(r.Path, [.. r.Calls.Select(c => new PinCallJson(c.Module, c.DeclType, c.MethodName, c.EntryName))]);

    private static RefusalJson MapRefusal(Refusal refusal) =>
        new(refusal.Path, refusal.Reason, InspectMap.Map(refusal.Before));

    private static void WriteBatch(PinBatch batch)
    {
        foreach (PinvokeRes r in batch.Results) WriteText(r);
        foreach (Refusal r in batch.Refusals)
            Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to inspect."
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
        }
    }
}
