using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace PeFix.Tests;

internal static class RefPe
{
    internal static void WriteVer(string outPath, string refName, Version refVer)
    {
        Write(outPath, refName, refVer, pkt: null);
    }

    internal static void WriteTok(string outPath, string refName, byte[] pkt)
    {
        Write(outPath, refName, new Version(1, 0, 0, 0), pkt);
    }

    private static void Write(string outPath, string refName, Version refVer, byte[]? pkt)
    {
        var meta = new MetadataBuilder();
        meta.AddModule(
            0,
            meta.GetOrAddString(Path.GetFileName(outPath)),
            meta.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        meta.AddTypeDefinition(
            0,
            default,
            meta.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        meta.AddAssembly(
            meta.GetOrAddString("test"),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            default);
        meta.AddAssemblyReference(
            meta.GetOrAddString(refName),
            refVer,
            default,
            pkt is null ? default : meta.GetOrAddBlob(pkt),
            default,
            default);

        var blob = new BlobBuilder();
        new MetadataRootBuilder(meta, suppressValidation: true).Serialize(blob, 0, 0);
        MiniPe.Write(outPath, blob.ToArray());
    }
}
