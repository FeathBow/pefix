using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Closure
{
    internal static CliExit Run(string path, bool json, bool failOnMissing)
    {
        return RunCore(path, json, new RunOpts(failOnMissing, false));
    }

    internal static CliExit RunTree(string path, bool json, bool failOnMissing)
    {
        return RunCore(path, json, new RunOpts(failOnMissing, true));
    }

    private static CliExit RunCore(string path, bool json, RunOpts opts)
    {
        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
            return CliErr.Usage($"Path must be a directory: {fullPath}");

        DirectoryInspection dir;
        try
        {
            dir = Scanner.InspectDir(fullPath);
        }
        catch (IOException ex)
        {
            return CliErr.Io(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CliErr.Io(ex);
        }

        ClosureReport closure = opts.Tree
            ? ClosureGraph.BuildTree(dir.Results, dir.Directory)
            : ClosureGraph.Build(dir.Results, dir.Directory);

        if (json)
        {
            JsonOut.Write(JsonWriter.Render(closure));
        }
        else
        {
            Console.WriteLine(ClosureOut.Render(closure));
        }

        if (opts.Fail && closure.Unresolved.Length > 0)
            return CliExit.Issue;

        return CliExit.Success;
    }

    private readonly record struct RunOpts(bool Fail, bool Tree);
}
