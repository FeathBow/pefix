namespace PeFix.Meta;

public readonly record struct ScanReport(
    string Directory,
    Inspection[] Results,
    VersionConflict[] Conflicts,
    MissingReference[] MissingReferences,
    DuplicateProvider[] DuplicateProviders,
    MemberRefGap[] MemberRefGaps,
    TypeRefGap[] TypeRefGaps,
    FieldRefGap[] FieldRefGaps,
    ImplGap[] ImplGaps,
    AccessGap[] AccessGaps);
