using System.Buffers.Binary;

namespace PeFix.Patch;

internal static class EcmaTables
{
    internal static int RowOffset(RowOffsetRequest request)
    {
        int table = (int)request.TableId;
        int pos = request.TableHeapOffset + 6;
        byte heapSizes = request.Bytes[pos++];
        pos++;
        ulong valid = BinaryPrimitives.ReadUInt64LittleEndian(request.Bytes.AsSpan(pos));
        pos += 8;
        pos += 8;

        int[] rowCounts = new int[64];
        for (int i = 0; i < 64; i++)
        {
            if ((valid & (1UL << i)) != 0)
            {
                rowCounts[i] = BinaryPrimitives.ReadInt32LittleEndian(request.Bytes.AsSpan(pos));
                pos += 4;
            }
        }

        if ((valid & (1UL << table)) == 0)
            throw new InvalidOperationException($"Metadata table 0x{table:X2} not present.");
        if (request.RowIndex < 1 || request.RowIndex > rowCounts[table])
            throw new ArgumentOutOfRangeException(nameof(request), $"Table 0x{table:X2} row {request.RowIndex} out of range [1..{rowCounts[table]}].");

        IdxWidths w = ComputeWidths(heapSizes, rowCounts);

        int tableStart = pos;
        for (int t = 0; t < table; t++)
        {
            if ((valid & (1UL << t)) != 0)
                tableStart += RowSize(t, w) * rowCounts[t];
        }

        return tableStart + (request.RowIndex - 1) * RowSize(table, w);
    }

    private static int RowSize(int table, IdxWidths w) => table switch
    {
        (int)TableId.Module => 2 + w.Str + w.Guid + w.Guid + w.Guid,
        (int)TableId.TypeRef => w.ResolutionScope + w.Str + w.Str,
        (int)TableId.TypeDef => 4 + w.Str + w.Str + w.TypeDefOrRef + w.TField + w.TMethod,
        (int)TableId.Field => 2 + w.Str + w.Blob,
        (int)TableId.MethodDef => 4 + 2 + 2 + w.Str + w.Blob + w.TParam,
        (int)TableId.Param => 2 + 2 + w.Str,
        (int)TableId.IfaceImpl => w.TTypeDef + w.TypeDefOrRef,
        (int)TableId.MemberRef => w.MemberRefParent + w.Str + w.Blob,
        (int)TableId.Const => 2 + w.HasConstant + w.Blob,
        (int)TableId.Attr => w.HasCustomAttribute + w.CustomAttributeType + w.Blob,
        (int)TableId.FieldMarshal => w.HasFieldMarshal + w.Blob,
        (int)TableId.DeclSec => 2 + w.HasDeclSecurity + w.Blob,
        (int)TableId.ClassLayout => 2 + 4 + w.TTypeDef,
        (int)TableId.FieldLayout => 4 + w.TField,
        (int)TableId.StandSig => w.Blob,
        (int)TableId.EventMap => w.TTypeDef + w.TEvent,
        (int)TableId.Event => 2 + w.Str + w.TypeDefOrRef,
        (int)TableId.PropMap => w.TTypeDef + w.TProperty,
        (int)TableId.Prop => 2 + w.Str + w.Blob,
        (int)TableId.MethodSem => 2 + w.TMethod + w.HasSemantics,
        (int)TableId.MethodImpl => w.TTypeDef + w.MethodDefOrRef + w.MethodDefOrRef,
        (int)TableId.ModuleRef => w.Str,
        (int)TableId.TypeSpec => w.Blob,
        (int)TableId.ImplMap => 2 + w.MemberForwarded + w.Str + w.TModuleRef,
        (int)TableId.FieldRva => 4 + w.TField,
        (int)TableId.Assembly => 4 + 2 + 2 + 2 + 2 + 4 + w.Blob + w.Str + w.Str,
        (int)TableId.AsmProc => 4,
        (int)TableId.AsmOs => 4 + 4 + 4,
        (int)TableId.AssemblyReference => 2 + 2 + 2 + 2 + 4 + w.Blob + w.Str + w.Str + w.Blob,
        _ => throw new InvalidOperationException($"Unsupported metadata table 0x{table:X2}.")
    };

    private static IdxWidths ComputeWidths(byte heapSizes, int[] rowCounts)
    {
        int Tbl(int table) => rowCounts[table] >= (1 << 16) ? 4 : 2;
        int Coded(int[] tables, int tagBits)
        {
            int max = 0;
            foreach (int t in tables)
                if (t < rowCounts.Length && rowCounts[t] > max) max = rowCounts[t];
            return max >= (1 << (16 - tagBits)) ? 4 : 2;
        }
        return new(
            (heapSizes & 0x01) != 0 ? 4 : 2,
            (heapSizes & 0x02) != 0 ? 4 : 2,
            (heapSizes & 0x04) != 0 ? 4 : 2,
            Coded([(int)TableId.Module, (int)TableId.ModuleRef, (int)TableId.AssemblyReference, (int)TableId.TypeRef], 2),
            Coded([(int)TableId.TypeDef, (int)TableId.TypeRef, (int)TableId.TypeSpec], 2),
            Coded([(int)TableId.Field, (int)TableId.Param, (int)TableId.Prop], 2),
            Coded([
                (int)TableId.MethodDef, (int)TableId.Field, (int)TableId.TypeRef, (int)TableId.TypeDef,
                (int)TableId.Param, (int)TableId.IfaceImpl, (int)TableId.MemberRef, (int)TableId.Module,
                (int)TableId.DeclSec, (int)TableId.Prop, (int)TableId.Event, (int)TableId.StandSig,
                (int)TableId.ModuleRef, (int)TableId.TypeSpec, (int)TableId.Assembly, (int)TableId.AssemblyReference,
                (int)TableId.File, (int)TableId.ExportType, (int)TableId.ManifestRes,
                (int)TableId.GenParam, (int)TableId.GenParamC, (int)TableId.MethodSpec
            ], 5),
            Coded([(int)TableId.Field, (int)TableId.Param], 1),
            Coded([(int)TableId.TypeDef, (int)TableId.MethodDef, (int)TableId.Assembly], 2),
            Coded([(int)TableId.TypeDef, (int)TableId.TypeRef, (int)TableId.ModuleRef, (int)TableId.MethodDef, (int)TableId.TypeSpec], 3),
            Coded([(int)TableId.Event, (int)TableId.Prop], 1),
            Coded([(int)TableId.MethodDef, (int)TableId.MemberRef], 1),
            Coded([(int)TableId.Field, (int)TableId.MethodDef], 1),
            Coded([(int)TableId.MethodDef, (int)TableId.MemberRef], 3),
            Tbl((int)TableId.Field),
            Tbl((int)TableId.MethodDef),
            Tbl((int)TableId.Param),
            Tbl((int)TableId.Event),
            Tbl((int)TableId.Prop),
            Tbl((int)TableId.TypeDef),
            Tbl((int)TableId.ModuleRef));
    }

    private readonly record struct IdxWidths(
        int Str, int Guid, int Blob,
        int ResolutionScope, int TypeDefOrRef, int HasConstant, int HasCustomAttribute,
        int HasFieldMarshal, int HasDeclSecurity, int MemberRefParent, int HasSemantics,
        int MethodDefOrRef, int MemberForwarded, int CustomAttributeType,
        int TField, int TMethod, int TParam, int TEvent, int TProperty, int TTypeDef, int TModuleRef);

    internal sealed record RowOffsetRequest
    {
        public required TableId TableId { get; init; }
        public required byte[] Bytes { get; init; }
        public required int TableHeapOffset { get; init; }
        public required int RowIndex { get; init; }
    }
}
