using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static class AttrReader
{
    internal static bool IsMatch(MetadataReader reader, CustomAttribute attr, string ns, string name)
    {
        return attr.Constructor.Kind switch
        {
            HandleKind.MemberReference => MatchesType(reader, reader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent, ns, name),
            HandleKind.MethodDefinition => MatchesType(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType(), ns, name),
            _ => false
        };
    }

    internal static string? ReadFixedString(CustomAttribute attr, int index)
    {
        CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
        if (decoded.FixedArguments.Length <= index)
            return null;

        object? value = decoded.FixedArguments[index].Value;
        return value switch
        {
            string text => text,
            null => throw new InvalidOperationException("Custom attribute fixed argument is null."),
            _ => throw new InvalidOperationException($"Unsupported custom attribute fixed argument type: {value.GetType().FullName}.")
        };
    }

    private static bool MatchesType(MetadataReader reader, EntityHandle handle, string ns, string name)
    {
        return handle.Kind switch
        {
            HandleKind.TypeReference => MatchesType(reader, reader.GetTypeReference((TypeReferenceHandle)handle), ns, name),
            HandleKind.TypeDefinition => MatchesType(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle), ns, name),
            _ => false
        };
    }

    private static bool MatchesType(MetadataReader reader, TypeReference typeRef, string ns, string name)
    {
        return MatchesType(reader, typeRef.Namespace, typeRef.Name, ns, name);
    }

    private static bool MatchesType(MetadataReader reader, TypeDefinition typeDef, string ns, string name)
    {
        return MatchesType(reader, typeDef.Namespace, typeDef.Name, ns, name);
    }

    private static bool MatchesType(MetadataReader reader, StringHandle nsHandle, StringHandle nameHandle, string ns, string name)
    {
        string nsValue = reader.GetString(nsHandle);
        string typeName = reader.GetString(nameHandle);
        return string.Equals(nsValue, ns, StringComparison.Ordinal)
            && string.Equals(typeName, name, StringComparison.Ordinal);
    }
}
