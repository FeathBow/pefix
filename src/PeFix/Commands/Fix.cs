using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Fix
{
    internal static int Run(string path, PatchOptions options, bool json)
    {
        return Directory.Exists(path)
            ? RunDirectory(path, options, json)
            : RunFile(path, options, json);
    }

    private static int RunFile(string path, PatchOptions options, bool json)
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
            return result.WasPatched ? 2 : 0;
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

            return 3;
        }
    }

    private static int RunDirectory(string path, PatchOptions options, bool json)
    {
        BatchResult result = BatchPatcher.Fix(path, options);
        if (json)
        {
            JsonOut.Write(JsonWriter.Render(result));
        }
        else
        {
            Console.WriteLine(BatchWriter.Render(result));
        }

        if (result.Results.Any(r => r.WasPatched))
        {
            return 2;
        }

        return result.Refusals.Length > 0 ? 3 : 0;
    }
}
