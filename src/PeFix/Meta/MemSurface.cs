namespace PeFix.Meta;

internal sealed class MemSurface(
    HashSet<string> typeNames,
    Dictionary<string, HashSet<MemberShape>> membersByType,
    Dictionary<string, HashSet<string>> fieldsByType,
    Dictionary<string, IfaceSurface> ifaceByType,
    HashSet<string> hiddenTypes,
    Dictionary<string, HashSet<MemberShape>> hiddenMembersByType,
    Dictionary<string, HashSet<string>> hiddenFieldsByType)
{
    public bool ContainsType(string typeName)
    {
        return typeNames.Contains(typeName);
    }

    public bool TryGetMembers(string typeName, out HashSet<MemberShape> members)
    {
        return membersByType.TryGetValue(typeName, out members!);
    }

    public bool TryGetFields(string typeName, out HashSet<string> fields)
    {
        return fieldsByType.TryGetValue(typeName, out fields!);
    }

    public bool TryGetIface(string typeName, out IfaceSurface surface)
    {
        return ifaceByType.TryGetValue(typeName, out surface!);
    }

    public bool IsHiddenType(string typeName)
    {
        return hiddenTypes.Contains(typeName);
    }

    public bool TryGetHiddenMembers(string typeName, out HashSet<MemberShape> members)
    {
        return hiddenMembersByType.TryGetValue(typeName, out members!);
    }

    public bool TryGetHiddenFields(string typeName, out HashSet<string> fields)
    {
        return hiddenFieldsByType.TryGetValue(typeName, out fields!);
    }
}
