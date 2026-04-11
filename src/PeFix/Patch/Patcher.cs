using PeFix.Meta;

namespace PeFix.Patch;

public static class Patcher
{
    public static PatchResult Fix(string path, PatchOptions options)
    {
        var fullPath = Path.GetFullPath(path);
        var before = PeAnalyzer.Inspect(fullPath);
        CheckSafe(before, options.Force);
        if (before.Status == Status.Compatible)
        {
            return new PatchResult(fullPath, null, before, before, false, false);
        }

        if (options.DryRun)
        {
            return new PatchResult(fullPath, null, before, before, false, true);
        }

        var backupPath = options.Backup ? CreateBackup(fullPath) : null;
        HdrPatcher.Patch(fullPath);
        var after = PeAnalyzer.Inspect(fullPath);
        CheckPatch(after, fullPath);
        Validator.Validate(fullPath);
        return new PatchResult(fullPath, backupPath, before, after, true, false);
    }

    public static PatchResult Fix(string path, bool backup = true, bool dryRun = false, bool force = false)
    {
        return Fix(path, new PatchOptions(backup, dryRun, force));
    }

    private static void CheckSafe(Inspection before, bool force)
    {
        if (before.Status is Status.Compatible or Status.Fixable)
        {
            return;
        }

        if (before.Status == Status.FixableWithWarnings && force)
        {
            return;
        }

        if (before.Status == Status.FixableWithWarnings)
        {
            throw new UnsafeException("This assembly requires --force because patching may invalidate the strong name signature or leave native dependencies unresolved.");
        }

        throw new UnsafeException($"This assembly cannot be patched safely ({CatText(before)}).");
    }

    private static string? CreateBackup(string path)
    {
        var backupPath = path + ".bak";
        File.Copy(path, backupPath, overwrite: true);
        return backupPath;
    }

    private static void CheckPatch(Inspection after, string path)
    {
        if (after.Status == Status.Compatible)
        {
            return;
        }

        throw new InvalidOperationException($"Patching {path} did not produce a compatible assembly.");
    }

    private static string CatText(Inspection before)
    {
        return before.Category switch
        {
            Category.ManagedPePortability => "managed_pe_portability",
            Category.ReferenceAssemblyMisuse => "reference_assembly_misuse",
            Category.NonRewritableBinary => "non_rewritable_binary",
            _ => "corrupt"
        };
    }
}
