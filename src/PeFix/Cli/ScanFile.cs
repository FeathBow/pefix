using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanFile(
    string ViewPath,
    string Category,
    Status Status,
    bool CanPatch,
    string Why,
    string Action,
    string ReasonCode,
    InspectJson? Json)
{
    public bool NeedsWork => Status != Status.Compatible;
}
