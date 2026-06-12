using PeFix.Meta;

namespace PeFix.Patch;

public static class Patcher
{
    public static PatchResult Fix(string path, PatchOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        Inspection before = PeAnalyzer.Inspect(fullPath);
        CheckSafe(before, options.Force);
        if (before.Status == Status.Compatible)
        {
            return new PatchResult(fullPath, null, before, before, false, false);
        }

        if (options.DryRun)
        {
            return new PatchResult(fullPath, null, before, before, false, true);
        }

        byte[] original = File.ReadAllBytes(fullPath);
        byte[] patched = HdrPatcher.Patch(original);
        string? backupPath = VerifiedWrite.ApplyNoPlan(new VerifiedWrite.NoPlan
        {
            Path = fullPath,
            Patched = patched,
            Backup = options.Backup,
            Verify = VerifyPatch
        });
        Inspection after = PeAnalyzer.Inspect(fullPath);
        CheckPatch(after, fullPath);
        return new PatchResult(fullPath, backupPath, before, after, true, false);
    }

    private static void CheckSafe(Inspection before, bool force)
    {
        if (before.Status is Status.Compatible or Status.Fixable)
        {
            return;
        }

        if (CanForcePatch(before, force))
        {
            return;
        }

        if (before.Status == Status.Cautioned)
        {
            throw new UnsafeException("This assembly requires --force because patching may invalidate the strong name signature or leave native dependencies unresolved.");
        }

        throw new UnsafeException($"This assembly cannot be patched safely ({Labels.CatText(before.Category)}).");
    }

    private static bool CanForcePatch(Inspection before, bool force)
    {
        return force
            && before.Status == Status.Cautioned
            && string.Equals(before.ReasonCode, ReasonCode.NonPortable, StringComparison.Ordinal);
    }

    private static void CheckPatch(Inspection after, string path)
    {
        if (after.Status == Status.Compatible)
        {
            return;
        }

        throw new InvalidOperationException($"Patching {path} did not produce a compatible assembly.");
    }

    private static void VerifyPatch(string path)
    {
        Inspection after = PeAnalyzer.Inspect(path);
        CheckPatch(after, path);
    }
}
