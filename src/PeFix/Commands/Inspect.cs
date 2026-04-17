using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Inspect
{
    internal static CliExit Run(string? path, bool asJson, string? failOn)
    {
        Status? threshold = null;
        if (failOn is not null)
        {
            if (!SevArg.TryParse(failOn, out Status value))
                return SevArg.WriteBad(failOn);

            threshold = value;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return CliErr.Io("A file or directory path is required.");
        }

        if (!File.Exists(path))
        {
            return CliErr.Io("A readable file path is required.");
        }

        Inspection result;
        try
        {
            result = PeAnalyzer.Inspect(path);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        if (asJson)
        {
            JsonOut.Write(JsonWriter.Render(result));
        }
        else
        {
            Console.Out.WriteLine(InspectOut.Render(result));
        }

        return GetExitCode(result, threshold);
    }

    private static CliExit GetExitCode(Inspection result, Status? threshold)
    {
        if (threshold is { } t)
            return result.Status >= t ? CliExit.Issue : CliExit.Success;
        return result.Status == Status.Compatible ? CliExit.Success : CliExit.Issue;
    }
}
