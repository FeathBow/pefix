using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Fix
{
    internal static CliExit Run(string path, PatchOptions options, bool json)
    {
        return Directory.Exists(path)
            ? RunDirectory(path, options, json)
            : RunFile(path, options, json);
    }

    private static CliExit RunFile(string path, PatchOptions options, bool json)
    {
        try
        {
            PatchResult result = Patcher.Fix(path, options);
            if (json)
            {
                JsonOut.Write(JsonWriter.Render(result));
            }
            else
            {
                Console.WriteLine(FixWriter.Render(result));
            }
            return CliExit.Success;
        }
        catch (UnsafeException ex)
        {
            if (json)
            {
                Inspection before = PeAnalyzer.Inspect(path);
                JsonOut.Write(JsonWriter.Render(new Refusal(path, ex.Message, before)));
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
            }

            return CliExit.Issue;
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

    private static CliExit RunDirectory(string path, PatchOptions options, bool json)
    {
        BatchResult result;
        try
        {
            result = BatchPatcher.Fix(path, options);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        if (json)
        {
            JsonOut.Write(JsonWriter.Render(result));
        }
        else
        {
            Console.WriteLine(BatchWriter.Render(result));
        }

        return result.Refusals.Length > 0
            ? CliExit.Issue
            : CliExit.Success;
    }
}
