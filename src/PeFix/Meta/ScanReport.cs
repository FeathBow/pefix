namespace PeFix.Meta;

public readonly record struct ScanReport(
    string Directory,
    Inspection[] Results,
    VerConflict[] Conflicts);
