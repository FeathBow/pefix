using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Redir
{
    internal static CliExit Run(RedirArgs args)
    {
        string? fromArg = args.FromArg;
        string? toArg = args.ToArg;
        if (string.IsNullOrEmpty(fromArg)) return CliErr.Usage("--from is required.");
        if (string.IsNullOrEmpty(toArg)) return CliErr.Usage("--to is required.");

        if (fromArg.Contains(','))
            return CliErr.Usage("Token, culture, and name changes are not supported. --from must be <name>:<version> only.");

        int colon = fromArg.IndexOf(':');
        if (colon < 0)
            return CliErr.Usage("--from must be in the form <name>:<version>.");

        string name = fromArg[..colon];
        string fromVerStr = fromArg[(colon + 1)..];

        if (!Version.TryParse(fromVerStr, out Version? fromVer) || fromVer.Build < 0 || fromVer.Revision < 0)
            return CliErr.Usage($"--from version '{fromVerStr}' must be 4-part (Major.Minor.Build.Revision).");

        if (!Version.TryParse(toArg, out Version? toVer) || toVer.Build < 0 || toVer.Revision < 0)
            return CliErr.Usage($"--to version '{toArg}' must be 4-part (Major.Minor.Build.Revision).");

        if (toVer.Major > ushort.MaxValue || toVer.Minor > ushort.MaxValue || toVer.Build > ushort.MaxValue || toVer.Revision > ushort.MaxValue)
            return CliErr.Usage("--to version fields must each be in [0..65535].");

        RedirOptions options = new(name, fromVer, toVer, args.Backup, args.DryRun);

        return PathRun.FileOrDir(
            args.Path,
            file => PathRun.TryFile(file, args.Json, () => RunFile(file, options, args.Json)),
            dir => PathRun.Try(() => RunDir(dir, options, args.Json)));
    }

    private static CliExit RunFile(string path, RedirOptions options, bool json)
    {
        RedirResult result = RedirPatch.Redir(path, options);
        if (json) JsonOut.Write(JsonWriter.Render(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static CliExit RunDir(string dir, RedirOptions options, bool json)
    {
        RedBatch batch = RedirPatch.RedirDir(dir, options);
        if (json)
            JsonOut.Write(JsonWriter.Render(batch));
        else
        {
            string dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string suffix = options.DryRun ? "" : " --apply";
            Console.WriteLine($"pefix redir {dirName}{suffix}");
            Console.WriteLine();
            Console.WriteLine($"  Summary: Scanned {batch.Results.Length + batch.Refusals.Length} candidate files.");
            Console.WriteLine();
            WriteBatch(batch);
        }
        return batch.Refusals.Length > 0 ? CliExit.Issue : CliExit.Success;
    }

    private static void WriteText(RedirResult r)
    {
        Console.WriteLine(RedirOut.Render(r));
    }

    private static void WriteBatch(RedBatch batch)
    {
        foreach (RedirResult r in batch.Results) WriteText(r);
        foreach (Refusal r in batch.Refusals)
            Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
    }

    internal sealed class RedirArgs
    {
        public required string Path { get; init; }
        public required string? FromArg { get; init; }
        public required string? ToArg { get; init; }
        public required bool Backup { get; init; }
        public required bool DryRun { get; init; }
        public required bool Json { get; init; }
    }
}
