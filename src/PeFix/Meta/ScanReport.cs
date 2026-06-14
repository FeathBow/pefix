namespace PeFix.Meta;

public readonly record struct ScanReport(
    string Directory,
    Inspection[] Results,
    GapSet Gaps,
    IReadOnlySet<string>? DeclaredAssets = null);
