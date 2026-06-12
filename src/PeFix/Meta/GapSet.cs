namespace PeFix.Meta;

public sealed class GapSet
{
    public static GapSet Empty { get; } = new();

    public VersionConflict[] Conflicts { get; init; } = [];

    public MissingReference[] MissingReferences { get; init; } = [];

    public DuplicateProvider[] DuplicateProviders { get; init; } = [];

    public MemberRefGap[] MemberRefGaps { get; init; } = [];

    public TypeRefGap[] TypeRefGaps { get; init; } = [];

    public FieldRefGap[] FieldRefGaps { get; init; } = [];

    public ImplGap[] ImplGaps { get; init; } = [];

    public AccessGap[] AccessGaps { get; init; } = [];

    public NativeGap[] NativeGaps { get; init; } = [];
}
