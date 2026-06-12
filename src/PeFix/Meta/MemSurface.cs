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

    public bool TryGetSurface(string typeName, out TypeSurface surface)
    {
        return surfaceByType.TryGetValue(typeName, out surface!);
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
}
