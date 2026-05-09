using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanFile(
    string ViewPath,
    string Category,
    Status Status,
    bool CanPatch,
    string Why,
    InspectJson Json)
{
    public bool NeedsWork => Status != Status.Compatible;

    public string Action => Json.Action;

    public string ReasonCode => Json.ReasonCode;
}
