namespace PeFix.Meta;

internal sealed class TypeSurface
{
    public required Dictionary<MemberShape, bool> Members { get; init; }

    public required Dictionary<string, bool> Fields { get; init; }

    public IfaceSurface? Iface { get; init; }

    public bool ContainsMember(MemberShape shape)
    {
        return Members.ContainsKey(shape);
    }

    public bool IsHiddenMember(MemberShape shape)
    {
        return Members.TryGetValue(shape, out bool visible) && !visible;
    }

    public bool ContainsField(string name)
    {
        return Fields.ContainsKey(name);
    }

    public bool IsHiddenField(string name)
    {
        return Fields.TryGetValue(name, out bool visible) && !visible;
    }
}
