using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Plan;

namespace PeFix.Patch;

public static class PublicPatch
{
    private const int MethFlagOfs = 6;

    public static PublicResult Publicize(string path, PubOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("Not a managed assembly.");
        MetadataReader reader = peReader.GetMetadataReader();

        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, metaRva);
        int tildeOfs = PeUtils.FindHeap(origBytes, metaOffset, "#~");

        byte[] patched = (byte[])origBytes.Clone();
        List<MutationOp> ops = [];
        HashSet<int> skipTypes = [];

        FlipTypes(reader, origBytes, patched, tildeOfs, ops, skipTypes);
        FlipMethods(reader, origBytes, patched, tildeOfs, ops, skipTypes);
        FlipFields(reader, origBytes, patched, tildeOfs, ops, skipTypes);

        if (options.DryRun)
            return new PublicResult(fullPath, null, null, true, ops.Count);

        if (ops.Count == 0)
            return new PublicResult(fullPath, null, null, false, 0);

        string? backupPath = options.Backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteAtomic(fullPath, patched);
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, patched, PeUtils.ReadMvid(patched)),
            ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);

        return new PublicResult(fullPath, backupPath, planPath, false, ops.Count);
    }

    private static void FlipTypes(MetadataReader reader, byte[] before, byte[] after, int tildeOfs, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (TypeDefinitionHandle h in reader.TypeDefinitions)
        {
            TypeDefinition td = reader.GetTypeDefinition(h);
            string name = reader.GetString(td.Name);
            int rowIdx = MetadataTokens.GetRowNumber(h);

            if (IsAngleName(name) || string.Equals(name, "<Module>", StringComparison.Ordinal))
            {
                skipTypes.Add(rowIdx);
                continue;
            }

            int rowOffset = EcmaTables.RowOffset(TableId.TypeDef, before, tildeOfs, rowIdx);
            uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(rowOffset, 4));
            TypeAttributes vis = (TypeAttributes)flagsBefore & TypeAttributes.VisibilityMask;
            TypeAttributes target = IsNested(vis) ? TypeAttributes.NestedPublic : TypeAttributes.Public;
            uint flagsAfter = (flagsBefore & ~(uint)TypeAttributes.VisibilityMask) | (uint)target;
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(rowOffset, 4), flagsAfter);
            ops.Add(MakeOp("typedef.flags", "TypeDef", rowIdx, rowOffset,
                before.AsSpan(rowOffset, 4), after.AsSpan(rowOffset, 4)));
        }
    }

    private static void FlipMethods(MetadataReader reader, byte[] before, byte[] after, int tildeOfs, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (MethodDefinitionHandle h in reader.MethodDefinitions)
        {
            MethodDefinition md = reader.GetMethodDefinition(h);
            int declaringRow = MetadataTokens.GetRowNumber(md.GetDeclaringType());
            if (skipTypes.Contains(declaringRow)) continue;

            string name = reader.GetString(md.Name);
            if (IsAngleName(name)) continue;

            int rowIdx = MetadataTokens.GetRowNumber(h);
            int rowOffset = EcmaTables.RowOffset(TableId.MethodDef, before, tildeOfs, rowIdx);
            int flagsOffset = rowOffset + MethFlagOfs;
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(flagsOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)MethodAttributes.MemberAccessMask) | (uint)MethodAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(flagsOffset, 2), flagsAfter);
            ops.Add(MakeOp("methoddef.flags", "MethodDef", rowIdx, flagsOffset,
                before.AsSpan(flagsOffset, 2), after.AsSpan(flagsOffset, 2)));
        }
    }

    private static void FlipFields(MetadataReader reader, byte[] before, byte[] after, int tildeOfs, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (FieldDefinitionHandle h in reader.FieldDefinitions)
        {
            FieldDefinition fd = reader.GetFieldDefinition(h);
            int declaringRow = MetadataTokens.GetRowNumber(fd.GetDeclaringType());
            if (skipTypes.Contains(declaringRow)) continue;

            string name = reader.GetString(fd.Name);
            if (IsAngleName(name)) continue;

            int rowIdx = MetadataTokens.GetRowNumber(h);
            int rowOffset = EcmaTables.RowOffset(TableId.Field, before, tildeOfs, rowIdx);
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(rowOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)FieldAttributes.FieldAccessMask) | (uint)FieldAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(rowOffset, 2), flagsAfter);
            ops.Add(MakeOp("field.flags", "Field", rowIdx, rowOffset,
                before.AsSpan(rowOffset, 2), after.AsSpan(rowOffset, 2)));
        }
    }

    private static MutationOp MakeOp(string targetKind, string tableName, int rowIdx, int offset, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after) =>
        new("publicize.flag",
            new PlanTarget(targetKind, Table: tableName, Row: rowIdx, Offset: offset),
            HexUtils.Hex(before),
            HexUtils.Hex(after));

    private static bool IsAngleName(string name) =>
        name.Length > 0 && name[0] == '<';

    private static bool IsNested(TypeAttributes vis) =>
        vis is TypeAttributes.NestedPublic
            or TypeAttributes.NestedPrivate
            or TypeAttributes.NestedFamily
            or TypeAttributes.NestedAssembly
            or TypeAttributes.NestedFamANDAssem
            or TypeAttributes.NestedFamORAssem;
}
