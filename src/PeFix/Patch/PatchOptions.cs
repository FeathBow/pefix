namespace PeFix.Patch;

public readonly record struct PatchOptions(
    bool Backup = true,
    bool DryRun = false,
    bool Force = false);
