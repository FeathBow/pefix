namespace PeFix.Meta;

internal sealed class TypeSurface
{
    public required HashSet<MemberShape> Members { get; init; }

    public required HashSet<string> Fields { get; init; }

    public HashSet<MemberShape>? HiddenMembers { get; init; }

    public HashSet<string>? HiddenFields { get; init; }

    public IfaceSurface? Iface { get; init; }
}
