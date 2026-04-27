namespace PeFix.Patch;

public readonly record struct SnStripOpts(
    bool Backup = true,
    bool DryRun = false,
    bool Force = false);
