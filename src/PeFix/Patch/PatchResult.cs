using PeFix.Meta;

namespace PeFix.Patch;

public readonly record struct PatchResult(
    string Path,
    string? BackupPath,
    Inspection Before,
    Inspection After,
    bool WasPatched,
    bool DryRun);
