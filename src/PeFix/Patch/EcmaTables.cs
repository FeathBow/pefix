using System.Buffers.Binary;

namespace PeFix.Patch;

internal static class EcmaTables
{
    private const int Module = 0x00;
    private const int TypeRef = 0x01;
    private const int TypeDef = 0x02;
    private const int Field = 0x04;
    private const int MethodDef = 0x06;
    private const int Param = 0x08;
    private const int InterfaceImpl = 0x09;
    private const int MemberRef = 0x0A;
    private const int Constant = 0x0B;
    private const int CustomAttribute = 0x0C;
    private const int FieldMarshal = 0x0D;
    private const int DeclSecurity = 0x0E;
    private const int ClassLayout = 0x0F;
    private const int FieldLayout = 0x10;
    private const int StandAloneSig = 0x11;
    private const int EventMap = 0x12;
    private const int Event = 0x14;
    private const int PropertyMap = 0x15;
    private const int Property = 0x17;
    private const int MethodSemantics = 0x18;
    private const int MethodImpl = 0x19;
    private const int ModuleRef = 0x1A;
    private const int TypeSpec = 0x1B;
    private const int ImplMap = 0x1C;
    private const int FieldRva = 0x1D;
    private const int Assembly = 0x20;
    private const int AssemblyProcessor = 0x21;
    private const int AssemblyOs = 0x22;
    private const int AssemblyRef = 0x23;

    internal static int AssemblyRefRowOffset(byte[] bytes, int tildeStream, int rowIdx)
    {
        int pos = tildeStream + 6;
        byte heapSizes = bytes[pos++];
        pos++;
        ulong valid = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(pos));
        pos += 8;
        pos += 8;

        int[] rowCounts = new int[64];
        for (int i = 0; i < 64; i++)
        {
            if ((valid & (1UL << i)) != 0)
            {
                rowCounts[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos));
                pos += 4;
            }
        }

        if ((valid & (1UL << AssemblyRef)) == 0)
            throw new InvalidOperationException("AssemblyRef table not present.");
        if (rowIdx < 1 || rowIdx > rowCounts[AssemblyRef])
            throw new ArgumentOutOfRangeException(nameof(rowIdx), $"AssemblyRef row {rowIdx} out of range [1..{rowCounts[AssemblyRef]}].");

        IdxWidths w = ComputeWidths(heapSizes, rowCounts);

        int tableStart = pos;
        for (int t = 0; t < AssemblyRef; t++)
        {
            if ((valid & (1UL << t)) != 0)
                tableStart += RowSize(t, w) * rowCounts[t];
        }

        return tableStart + (rowIdx - 1) * (2 + 2 + 2 + 2 + 4 + w.Blob + w.Str + w.Str + w.Blob);
    }

    private static int RowSize(int table, IdxWidths w) => table switch
    {
        Module => 2 + w.Str + w.Guid + w.Guid + w.Guid,
        TypeRef => w.ResolutionScope + w.Str + w.Str,
        TypeDef => 4 + w.Str + w.Str + w.TypeDefOrRef + w.TField + w.TMethod,
        Field => 2 + w.Str + w.Blob,
        MethodDef => 4 + 2 + 2 + w.Str + w.Blob + w.TParam,
        Param => 2 + 2 + w.Str,
        InterfaceImpl => w.TTypeDef + w.TypeDefOrRef,
        MemberRef => w.MemberRefParent + w.Str + w.Blob,
        Constant => 2 + w.HasConstant + w.Blob,
        CustomAttribute => w.HasCustomAttribute + w.CustomAttributeType + w.Blob,
        FieldMarshal => w.HasFieldMarshal + w.Blob,
        DeclSecurity => 2 + w.HasDeclSecurity + w.Blob,
        ClassLayout => 2 + 4 + w.TTypeDef,
        FieldLayout => 4 + w.TField,
        StandAloneSig => w.Blob,
        EventMap => w.TTypeDef + w.TEvent,
        Event => 2 + w.Str + w.TypeDefOrRef,
        PropertyMap => w.TTypeDef + w.TProperty,
        Property => 2 + w.Str + w.Blob,
        MethodSemantics => 2 + w.TMethod + w.HasSemantics,
        MethodImpl => w.TTypeDef + w.MethodDefOrRef + w.MethodDefOrRef,
        ModuleRef => w.Str,
        TypeSpec => w.Blob,
        ImplMap => 2 + w.MemberForwarded + w.Str + w.TModuleRef,
        FieldRva => 4 + w.TField,
        Assembly => 4 + 2 + 2 + 2 + 2 + 4 + w.Blob + w.Str + w.Str,
        AssemblyProcessor => 4,
        AssemblyOs => 4 + 4 + 4,
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
            Coded([Module, ModuleRef, AssemblyRef, TypeRef], 2),
            Coded([TypeDef, TypeRef, TypeSpec], 2),
            Coded([Field, Param, Property], 2),
            Coded([
                MethodDef, Field, TypeRef, TypeDef, Param, InterfaceImpl, MemberRef, Module,
                DeclSecurity, Property, Event, StandAloneSig, ModuleRef, TypeSpec, Assembly,
                AssemblyRef, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B
            ], 5),
            Coded([Field, Param], 1),
            Coded([TypeDef, MethodDef, Assembly], 2),
            Coded([TypeDef, TypeRef, ModuleRef, MethodDef, TypeSpec], 3),
            Coded([Event, Property], 1),
            Coded([MethodDef, MemberRef], 1),
            Coded([Field, MethodDef], 1),
            Coded([MethodDef, MemberRef], 3),
            Tbl(Field), Tbl(MethodDef), Tbl(Param), Tbl(Event), Tbl(Property), Tbl(TypeDef), Tbl(ModuleRef));
    }

    private readonly record struct IdxWidths(
        int Str, int Guid, int Blob,
        int ResolutionScope, int TypeDefOrRef, int HasConstant, int HasCustomAttribute,
        int HasFieldMarshal, int HasDeclSecurity, int MemberRefParent, int HasSemantics,
        int MethodDefOrRef, int MemberForwarded, int CustomAttributeType,
        int TField, int TMethod, int TParam, int TEvent, int TProperty, int TTypeDef, int TModuleRef);
}
