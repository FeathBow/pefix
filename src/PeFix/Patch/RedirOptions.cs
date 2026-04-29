namespace PeFix.Patch;

public readonly record struct RedirOptions(
    string Name,
    Version FromVersion,
    Version ToVersion,
    bool Backup = true,
    bool DryRun = false);
