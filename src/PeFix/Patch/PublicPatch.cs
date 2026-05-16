using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Plan;

namespace PeFix.Patch;

public static class PublicPatch
{
    private const int MethodFlagsOffset = 6;

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
        int tableHeapOffset = PeUtils.FindHeap(origBytes, metaOffset, "#~");

        byte[] patched = (byte[])origBytes.Clone();
        List<MutationOp> ops = [];
        HashSet<int> skipTypes = [];

        FlipTypes(reader, origBytes, patched, tableHeapOffset, ops, skipTypes);
        FlipMethods(reader, origBytes, patched, tableHeapOffset, ops, skipTypes);
        FlipFields(reader, origBytes, patched, tableHeapOffset, ops, skipTypes);

        if (options.DryRun)
            return new PublicResult(fullPath, null, null, true, ops.Count);

        if (ops.Count == 0)
            return new PublicResult(fullPath, null, null, false, 0);

        string? backupPath = options.Backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteVerifiedAtomic(fullPath, patched, tmpPath =>
        {
            VerifyPublicized(tmpPath, ops);
            Validator.Validate(tmpPath);
        });
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, patched, PeUtils.ReadMvid(patched)),
            ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);

        return new PublicResult(fullPath, backupPath, planPath, false, ops.Count);
    }

    private static void FlipTypes(MetadataReader reader, byte[] before, byte[] after, int tableHeapOffset, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = reader.GetString(type.Name);
            int rowIndex = MetadataTokens.GetRowNumber(handle);

            if (IsAngleName(name) || string.Equals(name, "<Module>", StringComparison.Ordinal))
            {
                skipTypes.Add(rowIndex);
                continue;
            }

            int rowOffset = EcmaTables.RowOffset(TableId.TypeDef, before, tableHeapOffset, rowIndex);
            uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(rowOffset, 4));
            TypeAttributes vis = (TypeAttributes)flagsBefore & TypeAttributes.VisibilityMask;
            TypeAttributes target = IsNested(vis) ? TypeAttributes.NestedPublic : TypeAttributes.Public;
            uint flagsAfter = (flagsBefore & ~(uint)TypeAttributes.VisibilityMask) | (uint)target;
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(rowOffset, 4), flagsAfter);
            ops.Add(MakeOp("typedef.flags", "TypeDef", rowIndex, rowOffset,
                before.AsSpan(rowOffset, 4), after.AsSpan(rowOffset, 4)));
        }
    }

    private static void FlipMethods(MetadataReader reader, byte[] before, byte[] after, int tableHeapOffset, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);
            int declaringRow = MetadataTokens.GetRowNumber(method.GetDeclaringType());
            if (skipTypes.Contains(declaringRow)) continue;

            string name = reader.GetString(method.Name);
            if (IsAngleName(name)) continue;

            int rowIndex = MetadataTokens.GetRowNumber(handle);
            int rowOffset = EcmaTables.RowOffset(TableId.MethodDef, before, tableHeapOffset, rowIndex);
            int flagsOffset = rowOffset + MethodFlagsOffset;
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(flagsOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)MethodAttributes.MemberAccessMask) | (uint)MethodAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(flagsOffset, 2), flagsAfter);
            ops.Add(MakeOp("methoddef.flags", "MethodDef", rowIndex, flagsOffset,
                before.AsSpan(flagsOffset, 2), after.AsSpan(flagsOffset, 2)));
        }
    }

    private static void FlipFields(MetadataReader reader, byte[] before, byte[] after, int tableHeapOffset, List<MutationOp> ops, HashSet<int> skipTypes)
    {
        foreach (FieldDefinitionHandle handle in reader.FieldDefinitions)
        {
            FieldDefinition field = reader.GetFieldDefinition(handle);
            int declaringRow = MetadataTokens.GetRowNumber(field.GetDeclaringType());
            if (skipTypes.Contains(declaringRow)) continue;

            string name = reader.GetString(field.Name);
            if (IsAngleName(name)) continue;

            int rowIndex = MetadataTokens.GetRowNumber(handle);
            int rowOffset = EcmaTables.RowOffset(TableId.Field, before, tableHeapOffset, rowIndex);
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(rowOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)FieldAttributes.FieldAccessMask) | (uint)FieldAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(rowOffset, 2), flagsAfter);
            ops.Add(MakeOp("field.flags", "Field", rowIndex, rowOffset,
                before.AsSpan(rowOffset, 2), after.AsSpan(rowOffset, 2)));
        }
    }

    private static MutationOp MakeOp(string targetKind, string tableName, int rowIndex, int offset, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after) =>
        new("publicize.flag",
            new PlanTarget(targetKind, Table: tableName, Row: rowIndex, Offset: offset),
            HexUtils.Hex(before),
            HexUtils.Hex(after));

    private static void VerifyPublicized(string path, IReadOnlyList<MutationOp> ops)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();

        foreach (MutationOp op in ops)
        {
            int row = RequireRow(op);
            VerifyPublicizedTarget(reader, op.Target.Kind, row);
        }
    }

    private static int RequireRow(MutationOp op)
    {
        if (op.Target.Row is { } row && row > 0)
            return row;

        throw new InvalidOperationException(
            $"publicize verification failed: target '{op.Target.Kind}' has no metadata row.");
    }

    private static void VerifyPublicizedTarget(MetadataReader reader, string targetKind, int row)
    {
        switch (targetKind)
        {
            case "typedef.flags":
                VerifyTypePublic(reader, row);
                break;
            case "methoddef.flags":
                VerifyMethodPublic(reader, row);
                break;
            case "field.flags":
                VerifyFieldPublic(reader, row);
                break;
            default:
                throw new InvalidOperationException(
                    $"publicize verification failed: unsupported target '{targetKind}' at row {row}.");
        }
    }

    private static void VerifyTypePublic(MetadataReader reader, int row)
    {
        TypeDefinition type = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row));
        TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
        if (visibility is TypeAttributes.Public or TypeAttributes.NestedPublic)
            return;

        throw new InvalidOperationException(
            $"publicize verification failed: TypeDef row {row} is {visibility}, expected public.");
    }

    private static void VerifyMethodPublic(MetadataReader reader, int row)
    {
        MethodDefinition method = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        MethodAttributes access = method.Attributes & MethodAttributes.MemberAccessMask;
        if (access == MethodAttributes.Public)
            return;

        throw new InvalidOperationException(
            $"publicize verification failed: MethodDef row {row} is {access}, expected public.");
    }

    private static void VerifyFieldPublic(MetadataReader reader, int row)
    {
        FieldDefinition field = reader.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(row));
        FieldAttributes access = field.Attributes & FieldAttributes.FieldAccessMask;
        if (access == FieldAttributes.Public)
            return;

        throw new InvalidOperationException(
            $"publicize verification failed: Field row {row} is {access}, expected public.");
    }

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
