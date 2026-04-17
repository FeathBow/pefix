using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Inspect
{
    internal static int Run(string? path, bool asJson, string? failOn)
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
            Console.Error.WriteLine("A file or directory path is required.");
            return 4;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine("A readable file path is required.");
            return 4;
        }

        Inspection result;
        try
        {
            result = PeAnalyzer.Inspect(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
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

    private static int GetExitCode(Inspection result, Status? threshold)
    {
        if (threshold is { } t)
            return result.Status >= t ? 1 : 0;
        return result.Status == Status.Compatible ? 0 : 1;
    }
}
