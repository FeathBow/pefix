using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace PeFix.Tests;

internal static class RefPe
{
    private const int FirstRow = 1;
    private const int NoMethodBody = 0;
    private const int NoMappedData = 0;
    private static readonly Version DefaultVersion = new(1, 0, 0, 0);

    private readonly record struct RefSpec(string Name, Version Version, byte[]? Token);

    internal static void WriteVersionRef(string outPath, string refName, Version refVersion)
    {
        Write(outPath, new RefSpec(refName, refVersion, Token: null));
    }

    internal static void WriteTokenRef(string outPath, string refName, byte[] publicKeyToken)
    {
        Write(outPath, new RefSpec(refName, DefaultVersion, publicKeyToken));
    }

    internal static void WriteNested(string outPath)
    {
        var meta = NewMetadata(outPath);
        TypeDefinitionHandle module = AddModuleType(meta);
        TypeDefinitionHandle nested = meta.AddTypeDefinition(
            TypeAttributes.NestedPrivate,
            default,
            meta.GetOrAddString("Nested"),
            default,
            FieldStart(),
            MethodStart());
        meta.AddNestedType(nested, module);
        Write(outPath, meta);
    }

    internal static void WriteMultiModule(string outPath)
    {
        var meta = NewMetadata(outPath);
        AddModuleType(meta);
        meta.AddAssemblyFile(
            meta.GetOrAddString("part.netmodule"),
            default,
            containsMetadata: true);
        Write(outPath, meta);
    }

    private static void Write(string outPath, RefSpec spec)
    {
        MetadataBuilder meta = NewMetadata(outPath);
        AddModuleType(meta);
        meta.AddAssemblyReference(
            meta.GetOrAddString(spec.Name),
            spec.Version,
            default,
            spec.Token is null ? default : meta.GetOrAddBlob(spec.Token),
            default,
            default);

        Write(outPath, meta);
    }

    private static MetadataBuilder NewMetadata(string outPath)
    {
        var meta = new MetadataBuilder();
        meta.AddModule(
            0,
            meta.GetOrAddString(Path.GetFileName(outPath)),
            meta.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        meta.AddAssembly(
            meta.GetOrAddString("test"),
            DefaultVersion,
            default,
            default,
            default,
            default);
        return meta;
    }

    private static TypeDefinitionHandle AddModuleType(MetadataBuilder meta)
    {
        return meta.AddTypeDefinition(
            0,
            default,
            meta.GetOrAddString("<Module>"),
            default,
            FieldStart(),
            MethodStart());
    }

    private static FieldDefinitionHandle FieldStart()
    {
        return MetadataTokens.FieldDefinitionHandle(FirstRow);
    }

    private static MethodDefinitionHandle MethodStart()
    {
        return MetadataTokens.MethodDefinitionHandle(FirstRow);
    }

    private static void Write(string outPath, MetadataBuilder meta)
    {
        var blob = new BlobBuilder();
        new MetadataRootBuilder(meta, suppressValidation: true).Serialize(blob, NoMethodBody, NoMappedData);
        MiniPe.Write(outPath, blob.ToArray());
    }
}
