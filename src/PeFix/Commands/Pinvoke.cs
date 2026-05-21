using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Pinvoke
{
    internal static CliExit Run(string path, bool json)
    {
        return PathRun.FileOrDir(
            path,
            file => PathRun.TryFile(file, json, () => RunFile(file, json)),
            dir => PathRun.Try(() => RunDir(dir, json)));
    }

    private static CliExit RunFile(string path, bool json)
    {
        PinvokeResult result = PinvokeScan.Inspect(path);
        if (json) JsonOut.Write(JsonWriter.Render(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static CliExit RunDir(string dir, bool json)
    {
        PinBatch batch = PinvokeScan.InspectDir(dir);
        if (json)
            JsonOut.Write(JsonWriter.Render(batch));
        else
        {
            string dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Console.WriteLine($"pefix pinvoke {dirName}");
            Console.WriteLine();
            Console.WriteLine($"  Summary: Scanned {batch.Results.Length + batch.Refusals.Length} candidate files.");
            Console.WriteLine();
            WriteBatch(batch);
        }
        return batch.Refusals.Length > 0 ? CliExit.Issue : CliExit.Success;
    }

    private static void WriteText(PinvokeResult r)
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

    private static void WriteBatch(PinBatch batch)
    {
        foreach (PinvokeResult r in batch.Results) WriteText(r);
        foreach (Refusal r in batch.Refusals)
            Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
    }
}
