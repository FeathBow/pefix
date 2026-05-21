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
            file => PathRun.TryFile(file, json, () => RunFile(file, options, json)),
            dir => PathRun.Try(() => RunDir(dir, options, json)));
    }

    private static CliExit RunFile(string path, SnStripOpts options, bool json)
    {
        try
        {
            SnStripResult result = SnStripper.Strip(path, options);
            if (json)
                JsonOut.Write(JsonWriter.Render(result));
            else
                WriteText(result);
            return result.DepFails.Length > 0 ? CliExit.Issue : CliExit.Success;
        }
        catch (UnsafeException ex)
        {
            if (json)
                JsonOut.Write(JsonWriter.Render(new Refusal(Path.GetFullPath(path), ex.Message, PeAnalyzer.Inspect(path))));
            else Console.Error.WriteLine(ex.Message);
            return CliExit.Issue;
        }
    }

    private static CliExit RunDir(string dir, SnStripOpts options, bool json)
    {
        SnBatch batch = SnStripper.StripDir(dir, options);
        if (json)
            JsonOut.Write(JsonWriter.Render(batch));
        else
        {
            string dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string suffix = options.DryRun ? "" : " --apply";
            Console.WriteLine($"pefix snstrip {dirName}{suffix}");
            Console.WriteLine();
            Console.WriteLine($"  Summary: Scanned {batch.Results.Length + batch.Refusals.Length} candidate files.");
            Console.WriteLine();
            foreach (SnStripResult r in batch.Results) WriteText(r);
            foreach (Refusal r in batch.Refusals) Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
            foreach (SnDependency dependency in batch.Deps) Console.WriteLine($"dep: {dependency.Path}");
        }

        return batch.Refusals.Length > 0
            ? CliExit.Issue
            : CliExit.Success;
    }

    private static void WriteText(SnStripResult r)
    {
        Console.WriteLine(SnStripOut.Render(r));
        if (r.HadSignedIvt)
            Console.Error.WriteLine("warning: InternalsVisibleTo uses a signed PublicKey");
        foreach (Refusal fail in r.DepFails)
            Console.Error.WriteLine($"refused: {fail.Path}: {fail.Reason}");
    }

}
