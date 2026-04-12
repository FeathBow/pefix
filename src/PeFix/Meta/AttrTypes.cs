using System.Reflection.Metadata;

namespace PeFix.Meta;

internal sealed class AttrTypes : ICustomAttributeTypeProvider<object?>
{
    public static readonly AttrTypes Instance = new();

    public object? GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
    public object? GetSystemType() => null;
    public object? GetSZArrayType(object? elementType) => null;
    public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;
    public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;
    public object? GetTypeFromSerializedName(string name) => null;
    public PrimitiveTypeCode GetUnderlyingEnumType(object? type) => PrimitiveTypeCode.Int32;
    public bool IsSystemType(object? type) => false;
}
