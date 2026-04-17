using System.CommandLine;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class PathCmd
{
    public static RootCommand Create()
    {
        var opts = new OptSet();
        var command = new RootCommand("Diagnose or fix managed assembly PE header portability issues.");
        opts.AddTo(command);
        command.SetAction(parseResult => Run(CreateReq(parseResult, opts)));

        return command;
    }

    public static void WriteStart()
    {
        Console.Out.WriteLine("pefix <path>");
        Console.Out.WriteLine("pefix <path> --fix");
        Console.Out.WriteLine();
        Console.Out.WriteLine("Examples:");
        Console.Out.WriteLine("  pefix MyMod.dll");
        Console.Out.WriteLine("  pefix ./mods");
        Console.Out.WriteLine("  pefix ./mods --fix --dry-run");
    }

    private static Req CreateReq(ParseResult parseResult, OptSet opts)
    {
        return new Req(
            parseResult.GetValue(opts.PathArg)!,
            parseResult.GetValue(opts.FixOpt),
            parseResult.GetValue(opts.JsonOpt),
            parseResult.GetValue(opts.FailOnOpt),
            parseResult.GetValue(opts.ConflictOpt),
            new PatchOptions(
                Backup: !parseResult.GetValue(opts.NoBackupOpt),
                DryRun: parseResult.GetValue(opts.DryRunOpt),
                Force: parseResult.GetValue(opts.ForceOpt)));
    }

    private static int Run(Req req)
    {
        return req.Fix
            ? RunFix(req)
            : RunRead(req);
    }

    private static int RunFix(Req req)
    {
        if (req.FailOn is not null)
            return Bad("Use --fail-on only without --fix.");

        if (req.OnConflict)
            return Bad("Use --fail-on-conflict only with a directory scan.");

        if (!File.Exists(req.Path) && !Directory.Exists(req.Path))
            return PathError();

        return Fix.Run(req.Path, req.Options, req.Json);
    }

    private static int RunRead(Req req)
    {
        if (req.Options.DryRun || req.Options.Force || !req.Options.Backup)
            return Bad("Use --dry-run, --force, and --no-backup only with --fix.");

        if (Directory.Exists(req.Path))
            return Scan.Run(req.Path, req.Json, req.FailOn, req.OnConflict);

        if (File.Exists(req.Path))
            return req.OnConflict
                ? Bad("Use --fail-on-conflict only with a directory scan.")
                : Inspect.Run(req.Path, req.Json, req.FailOn);

        return PathError();
    }

    private static int Bad(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static int PathError()
    {
        Console.Error.WriteLine("A readable file or directory path is required.");
        return 4;
    }

    private readonly record struct Req(
        string Path,
        bool Fix,
        bool Json,
        string? FailOn,
        bool OnConflict,
        PatchOptions Options);

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Assembly file or directory to inspect."
        };

        public Option<bool> FixOpt { get; } = new("--fix")
        {
            Description = "Attempt a safe PE header fix."
        };

        public Option<bool> JsonOpt { get; } = new("--json")
        {
            Description = "Write structured JSON output."
        };

        public Option<string?> FailOnOpt { get; } = new("--fail-on")
        {
            Description = "Exit with code 1 when the result meets or exceeds the given severity."
        };

        public Option<bool> ConflictOpt { get; } = new("--fail-on-conflict")
        {
            Description = "Exit with code 1 when version conflicts are detected in a directory scan."
        };

        public Option<bool> DryRunOpt { get; } = new("--dry-run")
        {
            Description = "Report fixes without modifying files."
        };

        public Option<bool> ForceOpt { get; } = new("--force")
        {
            Description = "Allow patching cautioned assemblies."
        };

        public Option<bool> NoBackupOpt { get; } = new("--no-backup")
        {
            Description = "Skip .bak file creation."
        };

        public void AddTo(Command command)
        {
            command.Arguments.Add(PathArg);
            command.Options.Add(FixOpt);
            command.Options.Add(JsonOpt);
            command.Options.Add(FailOnOpt);
            command.Options.Add(ConflictOpt);
            command.Options.Add(DryRunOpt);
            command.Options.Add(ForceOpt);
            command.Options.Add(NoBackupOpt);
        }
    }
}
