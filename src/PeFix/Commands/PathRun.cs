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
}
