using System.CommandLine;
using System.Text.Json;
using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Redir
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("redir", "Rewrite AssemblyRef version fields. Defaults to dry-run; pass --apply to write.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Run(
            r.GetValue(opts.PathArg)!,
            r.GetValue(opts.FromOpt),
            r.GetValue(opts.ToOpt),
            !r.GetValue(opts.NoBackupOpt),
            !r.GetValue(opts.ApplyOpt),
            r.GetValue(PathCmd.JsonOpt)));
        return cmd;
    }

    private static CliExit Run(string path, string? fromArg, string? toArg, bool backup, bool dryRun, bool json)
    {
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

        RedirOptions options = new(name, fromVer, toVer, backup, dryRun);

        return PathRun.FileOrDir(
            path,
            file => PathRun.Try(() => RunFile(file, options, json)),
            dir => PathRun.Try(() => RunDir(dir, options, json)));
    }

    private static CliExit RunFile(string path, RedirOptions options, bool json)
    {
        RedirResult result = RedirPatch.Redir(path, options);
        if (json) JsonOut.Write(ToJson(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static CliExit RunDir(string dir, RedirOptions options, bool json)
    {
        RedBatch batch = RedirPatch.RedirDir(dir, options);
        if (json)
            JsonOut.Write(ToJson(batch));
        else
            WriteBatch(batch);
        return batch.Refusals.Length > 0 ? CliExit.Issue : CliExit.Success;
    }

    private static void WriteText(RedirResult r)
    {
        Console.WriteLine(RedirWriter.Render(r));
    }

    private static string ToJson(RedirResult r) =>
        JsonSerializer.Serialize(ToJsonRecord(r), JsonContext.Default.RedirJson);

    private static string ToJson(RedBatch batch) =>
        JsonSerializer.Serialize(
            new RedBatchJson(
                batch.Directory,
                batch.Results.Select(ToJsonRecord).ToArray(),
                batch.Refusals.Select(MapRefusal).ToArray()),
            JsonContext.Default.RedBatchJson);

    private static RedirJson ToJsonRecord(RedirResult r) =>
        new(r.Path, r.BackupPath, r.PlanPath, r.WasDryRun, r.RowsPatched);

    private static RefusalJson MapRefusal(Refusal refusal) =>
        new(refusal.Path, refusal.Reason, InspectMap.Map(refusal.Before));

    private static void WriteBatch(RedBatch batch)
    {
        foreach (RedirResult r in batch.Results) WriteText(r);
        foreach (Refusal r in batch.Refusals)
            Console.Error.WriteLine($"refused: {r.Path}: {r.Reason}");
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to redirect."
        };
        public Option<string?> FromOpt { get; } = new("--from")
        {
            Description = "Match AssemblyRef in form <name>:<version>."
        };
        public Option<string?> ToOpt { get; } = new("--to")
        {
            Description = "Target version (Major.Minor.Build.Revision)."
        };
        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation."
        };
        public Option<bool> ApplyOpt { get; } = new("--apply")
        {
            Description = "Write changes to disk. Without this flag, the command only reports what would change.",
            DefaultValueFactory = _ => false
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(FromOpt);
            cmd.Options.Add(ToOpt);
            cmd.Options.Add(NoBackupOpt);
            cmd.Options.Add(ApplyOpt);
        }
    }
}
