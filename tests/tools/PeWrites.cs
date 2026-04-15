using System;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class PeWrites
{
    private const int MngHdrOff = 64;
    private const int FileAlign = 0x200;
    private const int SectAlign = 0x2000;
    private const int ImageBase = 0x10000000;
    private const int HeaderSize = FileAlign;
    private const int SectRva = SectAlign;
    private const int OptSize = 0xE0;
    private const int CliSize = 0x48;
    private const int SectSize = 40;
    private const int DirCount = 16;
    private const int CliDir = 14;
    private const ushort I386 = 0x014C;
    private const ushort OptMagic = 0x010B;
    private const ushort DllBits = 0x2102;
    private const ushort WinCui = 3;
    private const uint TextBits = 0x60000020;

    public static void WriteR2R(string sourcePath, string targetPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        using var readStream = new MemoryStream(bytes, writable: false);
        using var reader = new PEReader(readStream, PEStreamOptions.LeaveOpen);
        PEHeaders headers = reader.PEHeaders;
        byte[] r2rStub = [0x52, 0x54, 0x52, 0x00, 0x01, 0x00, 0x00, 0x00];
        int dirOffset = headers.CorHeaderStartOffset + MngHdrOff;
        int tableOffset = GetTableOff(headers, bytes);
        int sectIndex = FindPad(headers);
        if (sectIndex < 0)
        {
            throw new InvalidOperationException("No section has >= 8 bytes of raw padding to embed the R2R stub.");
        }

        SectionHeader section = headers.SectionHeaders[sectIndex];
        int fileOffset = (int)section.PointerToRawData + section.VirtualSize;
        int stubRva = (int)section.VirtualAddress + section.VirtualSize;
        r2rStub.CopyTo(bytes.AsSpan(fileOffset));
        SetVSize(bytes, tableOffset, sectIndex, section.VirtualSize + r2rStub.Length);
        SetDir(bytes, dirOffset, stubRva, r2rStub.Length);
        File.WriteAllBytes(targetPath, bytes);
    }

    public static void WriteConf(string targetPath)
    {
        byte[] meta = BuildMeta();
        byte[] pe = BuildPe(meta);
        File.WriteAllBytes(targetPath, pe);
    }

    private static int GetTableOff(PEHeaders headers, byte[] bytes)
    {
        int peStart = headers.PEHeaderStartOffset;
        ushort optSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(peStart - 4, 2));
        return peStart + optSize;
    }

    private static int FindPad(PEHeaders headers)
    {
        int bestIndex = -1;
        int bestPad = 0;
        for (int i = 0; i < headers.SectionHeaders.Length; i++)
        {
            SectionHeader section = headers.SectionHeaders[i];
            int pad = section.SizeOfRawData - section.VirtualSize;
            if (pad < 8 || pad <= bestPad)
                continue;

            bestPad = pad;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static void SetVSize(byte[] bytes, int tableOffset, int sectIndex, int value)
    {
        int fieldOffset = tableOffset + sectIndex * SectSize + 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(fieldOffset, 4), value);
    }

    private static void SetDir(byte[] bytes, int dirOffset, int rva, int size)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(dirOffset, 4), rva);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(dirOffset + 4, 4), size);
    }

    private static byte[] BuildMeta()
    {
        var meta = new MetadataBuilder();

        meta.AddModule(
            generation: 0,
            moduleName: meta.GetOrAddString("VerConflictConsumer.dll"),
            mvid: meta.GetOrAddGuid(new Guid("11111111-1111-1111-1111-111111111111")),
            encId: default,
            encBaseId: default);

        meta.AddTypeDefinition(
            attributes: 0,
            @namespace: default,
            name: meta.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        meta.AddAssembly(
            name: meta.GetOrAddString("VerConflictConsumer"),
            version: new Version(1, 0, 0, 0),
            culture: default,
            publicKey: default,
            flags: default,
            hashAlgorithm: default);

        meta.AddAssemblyReference(
            name: meta.GetOrAddString("CompatibleAnyCpu"),
            version: new Version(2, 0, 0, 0),
            culture: default,
            publicKeyOrToken: default,
            flags: default,
            hashValue: default);

        var metaBlob = new BlobBuilder();
        new MetadataRootBuilder(meta, suppressValidation: false)
            .Serialize(metaBlob, methodBodyStreamRva: 0, mappedFieldDataStreamRva: 0);
        return metaBlob.ToArray();
    }

    private static byte[] BuildPe(byte[] meta)
    {
        int rawSize = Align(CliSize + meta.Length, FileAlign);
        int imageSize = Align(SectRva + rawSize, SectAlign);
        int metaRva = SectRva + CliSize;
        var pe = new byte[HeaderSize + rawSize];
        var bytes = pe.AsSpan();
        WriteDos(bytes);
        int pos = 0x80;
        WriteSig(bytes, ref pos);
        WriteCoff(bytes, ref pos);
        WriteOpt(bytes, ref pos, rawSize, imageSize);
        WriteDirs(bytes, ref pos);
        WriteText(bytes, ref pos, meta.Length, rawSize);
        WriteCli(bytes.Slice(HeaderSize), metaRva, meta.Length);
        meta.CopyTo(bytes.Slice(HeaderSize + CliSize));
        return pe;
    }

    private static void WriteDos(Span<byte> bytes)
    {
        ReadOnlySpan<byte> stub = [
            0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
            0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00,
            0x0E, 0x1F, 0xBA, 0x0E, 0x00, 0xB4, 0x09, 0xCD,
            0x21, 0xB8, 0x01, 0x4C, 0xCD, 0x21, 0x54, 0x68,
            0x69, 0x73, 0x20, 0x70, 0x72, 0x6F, 0x67, 0x72,
            0x61, 0x6D, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F,
            0x74, 0x20, 0x62, 0x65, 0x20, 0x72, 0x75, 0x6E,
            0x20, 0x69, 0x6E, 0x20, 0x44, 0x4F, 0x53, 0x20,
            0x6D, 0x6F, 0x64, 0x65, 0x2E, 0x0D, 0x0D, 0x0A,
            0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        stub.CopyTo(bytes);
    }

    private static void WriteSig(Span<byte> bytes, ref int pos)
    {
        bytes[pos++] = 0x50;
        bytes[pos++] = 0x45;
        bytes[pos++] = 0x00;
        bytes[pos++] = 0x00;
    }

    private static void WriteCoff(Span<byte> bytes, ref int pos)
    {
        Write16(bytes, ref pos, I386);
        Write16(bytes, ref pos, 1);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0);
        Write16(bytes, ref pos, OptSize);
        Write16(bytes, ref pos, DllBits);
    }

    private static void WriteOpt(Span<byte> bytes, ref int pos, int rawSize, int imageSize)
    {
        Write16(bytes, ref pos, OptMagic);
        bytes[pos++] = 0x06;
        bytes[pos++] = 0x00;
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, (uint)rawSize);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, (uint)SectRva);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, (uint)ImageBase);
        Write32(bytes, ref pos, (uint)SectAlign);
        Write32(bytes, ref pos, (uint)FileAlign);
        Write16(bytes, ref pos, 4);
        Write16(bytes, ref pos, 0);
        Write16(bytes, ref pos, 0);
        Write16(bytes, ref pos, 0);
        Write16(bytes, ref pos, 4);
        Write16(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, (uint)imageSize);
        Write32(bytes, ref pos, (uint)HeaderSize);
        Write32(bytes, ref pos, 0);
        Write16(bytes, ref pos, WinCui);
        Write16(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0x100000);
        Write32(bytes, ref pos, 0x1000);
        Write32(bytes, ref pos, 0x100000);
        Write32(bytes, ref pos, 0x1000);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, DirCount);
    }

    private static void WriteDirs(Span<byte> bytes, ref int pos)
    {
        for (int i = 0; i < DirCount; i++)
        {
            if (i == CliDir)
            {
                Write32(bytes, ref pos, (uint)SectRva);
                Write32(bytes, ref pos, (uint)CliSize);
                continue;
            }

            Write32(bytes, ref pos, 0);
            Write32(bytes, ref pos, 0);
        }
    }

    private static void WriteText(Span<byte> bytes, ref int pos, int metaLen, int rawSize)
    {
        bytes[pos++] = 0x2E;
        bytes[pos++] = 0x74;
        bytes[pos++] = 0x65;
        bytes[pos++] = 0x78;
        bytes[pos++] = 0x74;
        bytes[pos++] = 0x00;
        bytes[pos++] = 0x00;
        bytes[pos++] = 0x00;
        Write32(bytes, ref pos, (uint)(CliSize + metaLen));
        Write32(bytes, ref pos, (uint)SectRva);
        Write32(bytes, ref pos, (uint)rawSize);
        Write32(bytes, ref pos, (uint)HeaderSize);
        Write32(bytes, ref pos, 0);
        Write32(bytes, ref pos, 0);
        Write16(bytes, ref pos, 0);
        Write16(bytes, ref pos, 0);
        Write32(bytes, ref pos, TextBits);
    }

    private static void WriteCli(Span<byte> bytes, int metaRva, int metaLen)
    {
        int pos = 0;
        Write32(bytes, ref pos, (uint)CliSize);
        Write16(bytes, ref pos, 2);
        Write16(bytes, ref pos, 5);
        Write32(bytes, ref pos, (uint)metaRva);
        Write32(bytes, ref pos, (uint)metaLen);
        Write32(bytes, ref pos, 1);
        Write32(bytes, ref pos, 0);
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    private static void Write16(Span<byte> bytes, ref int pos, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(pos, 2), value);
        pos += 2;
    }

    private static void Write32(Span<byte> bytes, ref int pos, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(pos, 4), value);
        pos += 4;
    }
}
