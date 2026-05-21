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
        return Publicize(path, options, VerifyPublicized);
    }

    internal static PublicResult Publicize(
        string path,
        PubOptions options,
        Action<string, IReadOnlyList<MutationOp>> verify)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new RefusalException("Not a managed assembly.");
        MetadataReader reader = peReader.GetMetadataReader();

        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, metaRva);
        int tableHeapOffset = PeUtils.FindHeap(origBytes, metaOffset, "#~");

        byte[] patched = (byte[])origBytes.Clone();
        var context = new PublicizeContext
        {
            Reader = reader,
            Before = origBytes,
            After = patched,
            TableHeapOffset = tableHeapOffset
        };
        FlipTypes(context);
        FlipMethods(context);
        FlipFields(context);

        if (options.DryRun)
            return new PublicResult(fullPath, null, null, true, context.Ops.ToArray());

        if (context.Ops.Count == 0)
            return new PublicResult(fullPath, null, null, false, []);

        VerifiedWriteResult write = VerifiedWrite.Apply(new VerifiedWrite.Request
        {
            Path = fullPath,
            Original = origBytes,
            Patched = patched,
            Ops = context.Ops,
            Backup = options.Backup,
            Verify = tmpPath => verify(tmpPath, context.Ops)
        });

        return new PublicResult(fullPath, write.BackupPath, write.PlanPath, false, context.Ops.ToArray());
    }

    private static void FlipTypes(PublicizeContext context)
    {
        foreach (TypeDefinitionHandle handle in context.Reader.TypeDefinitions)
        {
            TypeDefinition type = context.Reader.GetTypeDefinition(handle);
            string name = context.Reader.GetString(type.Name);
            int rowIndex = MetadataTokens.GetRowNumber(handle);

            if (IsAngleName(name) || string.Equals(name, "<Module>", StringComparison.Ordinal))
            {
                context.SkipTypes.Add(rowIndex);
                continue;
            }

            int rowOffset = EcmaTables.RowOffset(new EcmaTables.RowOffsetRequest
            {
                TableId = TableId.TypeDef,
                Bytes = context.Before,
                TableHeapOffset = context.TableHeapOffset,
                RowIndex = rowIndex
            });
            uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(context.Before.AsSpan(rowOffset, 4));
            TypeAttributes vis = (TypeAttributes)flagsBefore & TypeAttributes.VisibilityMask;
            TypeAttributes target = IsNested(vis) ? TypeAttributes.NestedPublic : TypeAttributes.Public;
            uint flagsAfter = (flagsBefore & ~(uint)TypeAttributes.VisibilityMask) | (uint)target;
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt32LittleEndian(context.After.AsSpan(rowOffset, 4), flagsAfter);
            context.Ops.Add(MakeOp(new PublicTarget
            {
                Kind = "typedef.flags",
                Table = "TypeDef",
                Row = rowIndex,
                Offset = rowOffset,
                Before = context.Before.AsSpan(rowOffset, 4).ToArray(),
                After = context.After.AsSpan(rowOffset, 4).ToArray()
            }));
        }
    }

    private static void FlipMethods(PublicizeContext context)
    {
        foreach (MethodDefinitionHandle handle in context.Reader.MethodDefinitions)
        {
            MethodDefinition method = context.Reader.GetMethodDefinition(handle);
            int declaringRow = MetadataTokens.GetRowNumber(method.GetDeclaringType());
            if (context.SkipTypes.Contains(declaringRow)) continue;

            string name = context.Reader.GetString(method.Name);
            if (IsAngleName(name)) continue;

            int rowIndex = MetadataTokens.GetRowNumber(handle);
            int rowOffset = EcmaTables.RowOffset(new EcmaTables.RowOffsetRequest
            {
                TableId = TableId.MethodDef,
                Bytes = context.Before,
                TableHeapOffset = context.TableHeapOffset,
                RowIndex = rowIndex
            });
            int flagsOffset = rowOffset + MethodFlagsOffset;
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(context.Before.AsSpan(flagsOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)MethodAttributes.MemberAccessMask) | (uint)MethodAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(context.After.AsSpan(flagsOffset, 2), flagsAfter);
            context.Ops.Add(MakeOp(new PublicTarget
            {
                Kind = "methoddef.flags",
                Table = "MethodDef",
                Row = rowIndex,
                Offset = flagsOffset,
                Before = context.Before.AsSpan(flagsOffset, 2).ToArray(),
                After = context.After.AsSpan(flagsOffset, 2).ToArray()
            }));
        }
    }

    private static void FlipFields(PublicizeContext context)
    {
        foreach (FieldDefinitionHandle handle in context.Reader.FieldDefinitions)
        {
            FieldDefinition field = context.Reader.GetFieldDefinition(handle);
            int declaringRow = MetadataTokens.GetRowNumber(field.GetDeclaringType());
            if (context.SkipTypes.Contains(declaringRow)) continue;

            string name = context.Reader.GetString(field.Name);
            if (IsAngleName(name)) continue;

            int rowIndex = MetadataTokens.GetRowNumber(handle);
            int rowOffset = EcmaTables.RowOffset(new EcmaTables.RowOffsetRequest
            {
                TableId = TableId.Field,
                Bytes = context.Before,
                TableHeapOffset = context.TableHeapOffset,
                RowIndex = rowIndex
            });
            ushort flagsBefore = BinaryPrimitives.ReadUInt16LittleEndian(context.Before.AsSpan(rowOffset, 2));
            ushort flagsAfter = (ushort)((flagsBefore & ~(uint)FieldAttributes.FieldAccessMask) | (uint)FieldAttributes.Public);
            if (flagsBefore == flagsAfter) continue;

            BinaryPrimitives.WriteUInt16LittleEndian(context.After.AsSpan(rowOffset, 2), flagsAfter);
            context.Ops.Add(MakeOp(new PublicTarget
            {
                Kind = "field.flags",
                Table = "Field",
                Row = rowIndex,
                Offset = rowOffset,
                Before = context.Before.AsSpan(rowOffset, 2).ToArray(),
                After = context.After.AsSpan(rowOffset, 2).ToArray()
            }));
        }
    }

    private static MutationOp MakeOp(PublicTarget target) =>
        new("publicize.flag",
            new PlanTarget(target.Kind, Table: target.Table, Row: target.Row, Offset: target.Offset),
            HexUtils.Hex(target.Before),
            HexUtils.Hex(target.After));

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

    private sealed class PublicizeContext
    {
        public required MetadataReader Reader { get; init; }
        public required byte[] Before { get; init; }
        public required byte[] After { get; init; }
        public required int TableHeapOffset { get; init; }
        public List<MutationOp> Ops { get; } = [];
        public HashSet<int> SkipTypes { get; } = [];
    }

    private sealed class PublicTarget
    {
        public required string Kind { get; init; }
        public required string Table { get; init; }
        public required int Row { get; init; }
        public required int Offset { get; init; }
        public required byte[] Before { get; init; }
        public required byte[] After { get; init; }
    }
}
