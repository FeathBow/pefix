namespace PeFix.Cli;

internal static class MutOut
{
    public static string RunStatus(bool wasDryRun, bool changed) => (wasDryRun, changed) switch
    {
        (true, _) => "DRY-RUN",
        (false, true) => "PATCHED",
        _ => "UNCHANGED",
    };

    public static void AddWriteDetails(
        List<(string, string)> details,
        bool wasDryRun,
        string path,
        string? backupPath,
        string? planPath)
    {
        if (wasDryRun)
        {
            details.Add(("Backup:", "Would write " + Path.GetFileName(path) + ".bak"));
        }
        else if (backupPath is not null)
        {
            details.Add(("Backup:", backupPath));
        }

        if (!wasDryRun && planPath is not null)
        {
            details.Add(("Plan:", planPath));
        }
    }

    public static string BackupAction(string? backupPath)
    {
        return backupPath is not null
            ? $"Backup written to {Path.GetFileName(backupPath)}. Run pefix scan <dir> --json to re-check the folder."
            : "Backup skipped (--no-backup). Run pefix scan <dir> --json to re-check the folder.";
    }
}
