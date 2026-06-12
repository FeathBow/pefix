namespace PeFix.Meta;

internal sealed class MemSurface(
    HashSet<string> typeNames,
    Dictionary<string, HashSet<MemberShape>> membersByType,
    Dictionary<string, HashSet<string>> fieldsByType)
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
}
