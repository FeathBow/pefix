namespace PeFix.Patch;

public readonly record struct PubOptions(
    bool Backup = true,
    bool DryRun = true);
