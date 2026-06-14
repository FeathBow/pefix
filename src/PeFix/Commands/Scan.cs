using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Scan
{
    internal static CliExit Run(ScanArgs args)
    {
        Status? threshold = null;
        if (args.FailOn is not null)
        {
            if (!SevArg.TryParse(args.FailOn, out Status value))
                return SevArg.WriteBad(args.FailOn);

            threshold = value;
        }

        if (!ProfileParser.TryParse(args.Profile, out ScanProfile? profile))
            return CliErr.Usage($"Unsupported scan profile: {args.Profile}");

        if (args.WriteBaseline && args.Baseline is null)
            return CliErr.Usage("--write-baseline requires --baseline <path>.");

        if (args.Baseline is { } knownPath && !args.WriteBaseline && !File.Exists(knownPath))
            return CliErr.Usage($"Baseline file not found: {knownPath}. Add --write-baseline to create it.");

        ScanReport report;
        try
        {
            report = Scanner.Scan(args.Path, profile?.Host);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        ScanResult scan = ScanBuild.Build(report, args.Json, profile, args.References);
        ScanView view = scan.View;

        if (ResolveBaseline(view, args, out BaselineDiff? diff, out string? baselineNote) is { } baselineError)
            return baselineError;

        WriteOutput(scan, view, args, diff, baselineNote);
        return Gate(args, view, diff, threshold);
    }

    private static CliExit? ResolveBaseline(ScanView view, ScanArgs args, out BaselineDiff? diff, out string? note)
    {
        diff = null;
        note = null;
        if (args.Baseline is not { } baselinePath)
            return null;

        string[] current = Baseline.Lines(view.GateIssues);
        if (args.WriteBaseline)
        {
            if (WriteBaselineFile(baselinePath, current) is { } error)
                return error;

            note = BaselineOut.RenderWritten(baselinePath, current.Length);
            return null;
        }

        if (!TryReadBaseline(baselinePath, out string[] known, out CliExit readError))
            return readError;

        diff = Baseline.Diff(current, Baseline.Parse(known));
        note = BaselineOut.Render(baselinePath, diff);
        return null;
    }

    private static void WriteOutput(ScanResult scan, ScanView view, ScanArgs args, BaselineDiff? diff, string? baselineNote)
    {
        if (args.Json)
        {
            ScanParts json = scan.Json
                ?? throw new InvalidOperationException("Scan JSON context was not built.");
            BaselineJson? baselineJson = diff is { } d && args.Baseline is { } path
                ? new BaselineJson(path, d.Matched, d.Fresh, d.Stale)
                : null;
            JsonOut.Write(JsonWriter.Render(view, json, baselineJson));
            return;
        }

        string text = ScanOut.Render(view, args.References);
        if (baselineNote is not null)
            text = $"{text}{Environment.NewLine}{Environment.NewLine}{baselineNote}";

        Console.WriteLine(text);
    }

    private static CliExit Gate(ScanArgs args, ScanView view, BaselineDiff? diff, Status? threshold)
    {
        if (args.FailOnConflict && view.Stats.HasConflict)
            return CliExit.Issue;

        if (args.FailOnIssue && ShouldFailOnIssue(view))
            return CliExit.Issue;

        if (diff is { } gateDiff && gateDiff.Fresh.Length > 0)
            return CliExit.Issue;

        return threshold is { } t && view.Files.Any(file => GateEval.Meets(file.Status, t))
            ? CliExit.Issue
            : CliExit.Success;
    }

    private static CliExit? WriteBaselineFile(string path, string[] lines)
    {
        try
        {
            File.WriteAllLines(path, lines);
            return null;
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }
    }

    private static bool TryReadBaseline(string path, out string[] known, out CliExit error)
    {
        try
        {
            known = File.ReadAllLines(path);
            error = CliExit.Success;
            return true;
        }
        catch (IOException ex)
        {
            known = [];
            error = CliErr.Io(ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            known = [];
            error = CliErr.Io(ex);
            return false;
        }
    }

    internal static bool ShouldFailOnIssue(ScanView view)
    {
        return view.HasGateIssues || view.HasBlockingFiles;
    }

    internal sealed class ScanArgs
    {
        public required string Path { get; init; }
        public required bool Json { get; init; }
        public required string? FailOn { get; init; }
        public required bool FailOnConflict { get; init; }
        public required bool FailOnIssue { get; init; }
        public required string? Profile { get; init; }
        public required bool References { get; init; }
        public required string? Baseline { get; init; }
        public required bool WriteBaseline { get; init; }
    }
}
