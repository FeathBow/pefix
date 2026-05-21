using PeFix.Cli;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Commands;

internal static class PathRun
{
    public static CliExit FileOnly(string path, Func<string, CliExit> run)
    {
        if (!System.IO.File.Exists(path))
            return CliErr.Io($"File not found: {path}");
        return run(path);
    }

    public static CliExit FileOrDir(string path, Func<string, CliExit> runFile, Func<string, CliExit> runDir)
    {
        if (Directory.Exists(path))
            return runDir(path);
        return FileOnly(path, runFile);
    }

    public static CliExit Try(Func<CliExit> run)
    {
        try { return run(); }
        catch (IOException ex) { return CliErr.Io(ex); }
        catch (UnauthorizedAccessException ex) { return CliErr.Io(ex); }
        catch (InvalidOperationException ex) { return CliErr.Io(ex.Message); }
    }

    public static CliExit TryFile(string path, bool json, Func<CliExit> run)
    {
        try { return run(); }
        catch (RefusalException ex) { return Refuse(path, ex.Message, json); }
        catch (BadImageFormatException ex) { return Refuse(path, ex.Message, json); }
        catch (IOException ex) { return CliErr.Io(ex); }
        catch (UnauthorizedAccessException ex) { return CliErr.Io(ex); }
        catch (InvalidOperationException ex) { return CliErr.Io(ex.Message); }
    }

    private static CliExit Refuse(string path, string reason, bool json)
    {
        if (json)
        {
            Inspection before = PeAnalyzer.Inspect(path);
            JsonOut.Write(JsonWriter.Render(new Refusal(Path.GetFullPath(path), reason, before)));
        }
        else
        {
            Console.Error.WriteLine(reason);
        }

        return CliExit.Issue;
    }
}
