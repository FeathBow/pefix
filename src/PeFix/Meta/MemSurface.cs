namespace PeFix.Meta;

internal sealed class MemSurface(
    HashSet<string> typeNames,
    Dictionary<string, TypeSurface> surfaceByType,
    HashSet<string> hiddenTypes)
{
    public bool ContainsType(string typeName)
    {
        return typeNames.Contains(typeName);
    }

    public bool TryGetMembers(string typeName, out HashSet<MemberShape> members)
    {
        members = null!;
        if (!surfaceByType.TryGetValue(typeName, out TypeSurface? surface))
            return false;

        members = surface.Members;
        return true;
    }

    public bool TryGetFields(string typeName, out HashSet<string> fields)
    {
        fields = null!;
        if (!surfaceByType.TryGetValue(typeName, out TypeSurface? surface))
            return false;

        fields = surface.Fields;
        return true;
    }

    public bool TryGetIface(string typeName, out IfaceSurface surface)
    {
        surface = null!;
        if (!surfaceByType.TryGetValue(typeName, out TypeSurface? typeSurface) || typeSurface.Iface is null)
            return false;

        surface = typeSurface.Iface;
        return true;
    }

    public bool IsHiddenType(string typeName)
    {
        return hiddenTypes.Contains(typeName);
    }

    public bool TryGetHiddenMembers(string typeName, out HashSet<MemberShape> members)
    {
        members = null!;
        if (!surfaceByType.TryGetValue(typeName, out TypeSurface? surface) || surface.HiddenMembers is null)
            return false;

        members = surface.HiddenMembers;
        return true;
    }

    public bool TryGetHiddenFields(string typeName, out HashSet<string> fields)
    {
        fields = null!;
        if (!surfaceByType.TryGetValue(typeName, out TypeSurface? surface) || surface.HiddenFields is null)
            return false;

        fields = surface.HiddenFields;
        return true;
    }
}
