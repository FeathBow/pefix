using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Closure
{
    internal static CliExit Run(string path, bool json, bool failOnMissing)
    {
        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
            return CliErr.Usage($"Path must be a directory: {fullPath}");

        DirInspect dir;
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

        ClosureReport closure = ClosureGraph.Build(dir.Results, dir.Directory);

        if (json)
        {
            JsonOut.Write(JsonWriter.Render(closure));
        }
        else
        {
            Console.WriteLine(ClosureOut.Render(closure));
        }

        if (failOnMissing && closure.Unresolved.Length > 0)
            return CliExit.Issue;

        return CliExit.Success;
    }
}
