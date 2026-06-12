using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static partial class MemberSurfaceAnalyzer
{
    private sealed class ParamCounter : ISignatureTypeProvider<int, object?>
    {
        public int GetArrayType(int elementType, ArrayShape shape) => elementType;
        public int GetByReferenceType(int elementType) => elementType;
        public int GetFunctionPointerType(MethodSignature<int> signature) => signature.ParameterTypes.Length;
        public int GetGenericInstantiation(int genericType, ImmutableArray<int> typeArguments) => genericType;
        public int GetGenericMethodParameter(object? genericContext, int index) => index;
        public int GetGenericTypeParameter(object? genericContext, int index) => index;
        public int GetModifiedType(int modifierType, int unmodifiedType, bool isRequired) => unmodifiedType;
        public int GetPinnedType(int elementType) => elementType;
        public int GetPointerType(int elementType) => elementType;
        public int GetPrimitiveType(PrimitiveTypeCode typeCode) => 0;
        public int GetSZArrayType(int elementType) => elementType;
        public int GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => 0;
        public int GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => 0;
        public int GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => 0;
    }
}
