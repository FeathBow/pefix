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

        ScanResult scan = ScanBuild.Build(report, args.Json, profile);
        ScanView view = scan.View;

        if (args.Json)
        {
            ScanJsonParts json = scan.Json
                ?? throw new InvalidOperationException("Scan JSON context was not built.");
            JsonOut.Write(JsonWriter.Render(view, json));
        }
        else
        {
            Console.WriteLine(ScanOut.Render(view));
        }

        if (args.FailOnConflict && view.Stats.HasConflict)
            return CliExit.Issue;

        if (args.FailOnIssue && HasBlockingIssue(view))
            return CliExit.Issue;

        return threshold is { } t && view.Files.Any(file => GateEval.Meets(file.Status, t))
            ? CliExit.Issue
            : CliExit.Success;
    }

    private static bool HasBlockingIssue(ScanView view)
    {
        return view.HasIssues || view.HasBlockingFiles;
    }

    internal sealed class ScanArgs
    {
        public required string Path { get; init; }
        public required bool Json { get; init; }
        public required string? FailOn { get; init; }
        public required bool FailOnConflict { get; init; }
        public required bool FailOnIssue { get; init; }
        public required string? Profile { get; init; }
    }
}
