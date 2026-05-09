using System.CommandLine;
using System.Text.Json;
using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class SnStrip
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("snstrip", "Strip strong-name signing from a managed assembly or directory.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Run(
            r.GetValue(opts.PathArg)!,
            new SnStripOpts(
                Backup: !r.GetValue(opts.NoBackupOpt),
                DryRun: r.GetValue(opts.DryRunOpt),
                Force: r.GetValue(opts.ForceOpt)),
            r.GetValue(opts.JsonOpt)));
        return cmd;
    }

    private static CliExit Run(string path, SnStripOpts options, bool json)
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
        if (r.WasDryRun)
        {
            Console.WriteLine($"dry-run: {r.Path}");
            if (r.HadSignedIvt) Console.WriteLine("  warning: InternalsVisibleTo uses a signed PublicKey");
            return;
        }
        if (!r.WasPatched)
        {
            Console.WriteLine($"unchanged: {r.Path}");
            return;
        }
        Console.WriteLine($"stripped: {r.Path}");
        if (r.BackupPath is not null) Console.WriteLine($"  backup:  {r.BackupPath}");
        if (r.PlanPath is not null) Console.WriteLine($"  plan:    {r.PlanPath}");
        if (r.HadSignedIvt) Console.WriteLine("  warning: InternalsVisibleTo uses a signed PublicKey");
        if (r.DepsPatched > 0)
        {
            Console.WriteLine($"  deps:    {r.DepsPatched} sibling DLL(s) patched");
            foreach (SnDep dep in r.Deps)
                Console.WriteLine($"  dep:     {dep.Path}");
        }
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

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to strip."
        };
        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation."
        };
        public Option<bool> DryRunOpt { get; } = new("--dry-run")
        {
            Description = "Report without modifying the file."
        };
        public Option<bool> ForceOpt { get; } = new("--force")
        {
            Description = "Strip even when InternalsVisibleTo uses a signed PublicKey."
        };
        public Option<bool> JsonOpt { get; } = new("--json")
        {
            Description = "Write structured JSON output."
        };
        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(NoBackupOpt);
            cmd.Options.Add(DryRunOpt);
            cmd.Options.Add(ForceOpt);
            cmd.Options.Add(JsonOpt);
        }
    }
}
