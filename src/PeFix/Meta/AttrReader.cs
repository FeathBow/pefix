using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static class AttrReader
{
    internal static bool IsMatch(MetadataReader reader, CustomAttribute attr, string ns, string name)
    {
        return IsMatch(reader, attr, new AttrType(ns, name));
    }

    internal static bool IsMatch(MetadataReader reader, CustomAttribute attr, AttrType type)
    {
        return attr.Constructor.Kind switch
        {
            HandleKind.MemberReference => MatchesType(reader, reader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent, type),
            HandleKind.MethodDefinition => MatchesType(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType(), type),
            _ => false
        };
    }

    internal static string? ReadFixedString(CustomAttribute attr, int index)
    {
        object? value = ReadFixedValue(attr, index);
        return StringValue(value);
    }

    internal static string? ReadNamedString(CustomAttribute attr, string name)
    {
        return ReadNamedString(attr.DecodeValue(AttrTypes.Instance), name);
    }

    internal static string? ReadNamedString(CustomAttributeValue<object?> decoded, string name)
    {
        object? value = ReadNamedValue(decoded, name);
        return StringValue(value);
    }

    internal static int? ReadNamedInt(CustomAttribute attr, string name)
    {
        return ReadNamedInt(attr.DecodeValue(AttrTypes.Instance), name);
    }

    internal static int? ReadNamedInt(CustomAttributeValue<object?> decoded, string name)
    {
        object? value = ReadNamedValue(decoded, name);
        return value switch
        {
            MissingArg => null,
            int number => number,
            null => throw new InvalidOperationException("Custom attribute named argument is null."),
            _ => throw new InvalidOperationException($"Unsupported custom attribute named argument type: {value.GetType().FullName}.")
        };
    }

    private static string? StringValue(object? value)
    {
        return value switch
        {
            string text => text,
            MissingArg => null,
            null => throw new InvalidOperationException("Custom attribute string argument is null."),
            _ => throw new InvalidOperationException($"Unsupported custom attribute string argument type: {value.GetType().FullName}.")
        };
    }

    private static object? ReadFixedValue(CustomAttribute attr, int index)
    {
        CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
        if (decoded.FixedArguments.Length <= index)
            return MissingArg.Instance;

        return decoded.FixedArguments[index].Value;
    }

    private static object? ReadNamedValue(CustomAttributeValue<object?> decoded, string name)
    {
        return decoded.NamedArguments
            .Where(arg => string.Equals(arg.Name, name, StringComparison.Ordinal))
            .Select(arg => arg.Value)
            .DefaultIfEmpty(MissingArg.Instance)
            .First();
    }

    private sealed class MissingArg
    {
        internal static readonly MissingArg Instance = new();
    }

    private static bool MatchesType(MetadataReader reader, EntityHandle handle, AttrType type)
    {
        return handle.Kind switch
        {
            HandleKind.TypeReference => MatchesType(reader, reader.GetTypeReference((TypeReferenceHandle)handle), type),
            HandleKind.TypeDefinition => MatchesType(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle), type),
            _ => false
        };
    }

    private static bool MatchesType(MetadataReader reader, TypeReference typeRef, AttrType type)
    {
        return MatchesType(new AttrRef(reader, typeRef.Namespace, typeRef.Name), type);
    }

    private static bool MatchesType(MetadataReader reader, TypeDefinition typeDef, AttrType type)
    {
        return MatchesType(new AttrRef(reader, typeDef.Namespace, typeDef.Name), type);
    }

    private static bool MatchesType(AttrRef attr, AttrType type)
    {
        string nsValue = attr.Reader.GetString(attr.Namespace);
        string typeName = attr.Reader.GetString(attr.Name);
        return string.Equals(nsValue, type.Namespace, StringComparison.Ordinal)
            && string.Equals(typeName, type.Name, StringComparison.Ordinal);
    }

    internal readonly record struct AttrType(string Namespace, string Name);

    private readonly record struct AttrRef(MetadataReader Reader, StringHandle Namespace, StringHandle Name);
}
