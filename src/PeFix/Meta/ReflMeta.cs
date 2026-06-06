using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace PeFix.Meta;

internal static partial class ReflScanner
{
    private static bool TryReadUserString(
        MetadataReader reader,
        int token,
        out string value)
    {
        value = string.Empty;
        try
        {
            Handle handle = MetadataTokens.Handle(token);
            if (handle.Kind != HandleKind.UserString)
                return false;

            value = reader.GetUserString((UserStringHandle)handle);
            return IsReadable(value);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool TryReadMethodTarget(
        MetadataReader reader,
        int token,
        out MethodTarget target)
    {
        target = default;
        try
        {
            return TryReadMethodTarget(reader, MetadataTokens.EntityHandle(token), out target);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadMethodTarget(
        MetadataReader reader,
        EntityHandle handle,
        out MethodTarget target)
    {
        target = default;
        return handle.Kind switch
        {
            HandleKind.MemberReference => TryReadMemberRefTarget(reader, (MemberReferenceHandle)handle, out target),
            HandleKind.MethodDefinition => TryReadMethodDefTarget(reader, (MethodDefinitionHandle)handle, out target),
            HandleKind.MethodSpecification => TryReadMethodSpecTarget(reader, (MethodSpecificationHandle)handle, out target),
            _ => false
        };
    }

    private static bool TryReadMemberRefTarget(
        MetadataReader reader,
        MemberReferenceHandle handle,
        out MethodTarget target)
    {
        target = default;
        MemberReference member = reader.GetMemberReference(handle);
        if (!TryReadTypeName(reader, member.Parent, out string typeName))
            return false;

        target = new MethodTarget(typeName, reader.GetString(member.Name));
        return true;
    }

    private static bool TryReadMethodDefTarget(
        MetadataReader reader,
        MethodDefinitionHandle handle,
        out MethodTarget target)
    {
        target = default;
        MethodDefinition method = reader.GetMethodDefinition(handle);
        TypeDefinition type = reader.GetTypeDefinition(method.GetDeclaringType());
        target = new MethodTarget(TypeName(reader, type.Namespace, type.Name), reader.GetString(method.Name));
        return true;
    }

    private static bool TryReadMethodSpecTarget(
        MetadataReader reader,
        MethodSpecificationHandle handle,
        out MethodTarget target)
    {
        MethodSpecification spec = reader.GetMethodSpecification(handle);
        return TryReadMethodTarget(reader, spec.Method, out target);
    }

    private static bool TryReadTypeName(
        MetadataReader reader,
        EntityHandle handle,
        out string typeName)
    {
        typeName = string.Empty;
        if (handle.Kind == HandleKind.TypeReference)
        {
            TypeReference type = reader.GetTypeReference((TypeReferenceHandle)handle);
            typeName = TypeName(reader, type.Namespace, type.Name);
            return true;
        }

        if (handle.Kind != HandleKind.TypeDefinition)
            return false;

        TypeDefinition definition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
        typeName = TypeName(reader, definition.Namespace, definition.Name);
        return true;
    }

    private static string TypeName(
        MetadataReader reader,
        StringHandle ns,
        StringHandle name)
    {
        string nsValue = reader.GetString(ns);
        string typeName = reader.GetString(name);
        return nsValue.Length == 0 ? typeName : $"{nsValue}.{typeName}";
    }

    private static bool IsReadable(string value)
    {
        return value.Length > 0
            && value.All(ch => !char.IsControl(ch) || char.IsWhiteSpace(ch));
    }
}
