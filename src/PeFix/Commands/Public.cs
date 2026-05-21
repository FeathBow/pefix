using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Public
{
    internal static CliExit Run(string path, PubOptions options, bool json)
    {
        return PathRun.FileOnly(path, file => PathRun.TryFile(file, json, () => RunFile(file, options, json)));
    }

    private static CliExit RunFile(string path, PubOptions options, bool json)
    {
        PublicResult result = PublicPatch.Publicize(path, options);
        if (json) JsonOut.Write(JsonWriter.Render(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static void WriteText(PublicResult r)
    {
        Console.WriteLine(PublicOut.Render(r));
    }

}
