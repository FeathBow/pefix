using System.Text.Json;
using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class Public
{
    internal static CliExit Run(string path, PubOptions options, bool json)
    {
        return PathRun.FileOnly(path, file => PathRun.Try(() => RunFile(file, options, json)));
    }

    private static CliExit RunFile(string path, PubOptions options, bool json)
    {
        PublicResult result = PublicPatch.Publicize(path, options);
        if (json) JsonOut.Write(ToJson(result));
        else WriteText(result);
        return CliExit.Success;
    }

    private static void WriteText(PublicResult r)
    {
        Console.WriteLine(PublicWriter.Render(r));
    }

    private static string ToJson(PublicResult r) =>
        JsonSerializer.Serialize(
            new PublicJson(r.Path, r.BackupPath, r.PlanPath, r.WasDryRun, r.OpsCount),
            JsonContext.Default.PublicJson);
}
