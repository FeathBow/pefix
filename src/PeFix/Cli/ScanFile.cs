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
}
