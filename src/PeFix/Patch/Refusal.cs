using PeFix.Meta;

namespace PeFix.Patch;

public readonly record struct Refusal(
    string Path,
    string Reason,
    Inspection Before);
