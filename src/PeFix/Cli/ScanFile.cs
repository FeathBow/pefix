using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanFile(
    string ViewPath,
    string Category,
    Status Status,
    bool CanPatch,
    string ReasonText,
    string ActionText,
    string ReasonCode)
{
    public bool NeedsWork => Status != Status.Compatible;

    // One gate-blocking rule, shared by ScanView (exit code) and MetricBuild (JSON gate).
    public bool IsBlocking => Status is Status.Unsafe or Status.Corrupt;
}
