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

        ScanReport report;
        try
        {
            report = Scanner.Scan(args.Path);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        ScanView view = ScanBuild.Build(report, args.Json);

        if (args.Json)
        {
            JsonOut.Write(JsonWriter.Render(view));
        }
        else
        {
            Console.WriteLine(ScanOut.Render(view));
        }

        if (args.FailOnConflict && view.Stats.HasConflict)
            return CliExit.Issue;

        return threshold is { } t && view.Files.Any(file => GateEval.Meets(file.Status, t))
            ? CliExit.Issue
            : CliExit.Success;
    }

    internal sealed class ScanArgs
    {
        public required string Path { get; init; }
        public required bool Json { get; init; }
        public required string? FailOn { get; init; }
        public required bool FailOnConflict { get; init; }
    }
}
