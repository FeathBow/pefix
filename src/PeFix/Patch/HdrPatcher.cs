using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace PeFix.Patch;

internal static class HdrPatcher
{
    private const int CoffHdrSize = 20;
    private const int MachOff = 0;
    private const int OptSizeOff = 16;
    private const int CharsOff = 18;
    private const int SectHdrSize = 40;
    private const int SectVaOff = 12;
    private const int SectCharsOff = 36;
    private const int SectSzOff = 8;
    private const int Pe32OptSize = 224;
    private const ushort Pe32Magic = 0x10B;
    private const ushort I386Machine = 0x14C;
    private const ushort Img32BitMach = 0x0100;
    private const ushort BigAddrAware = 0x0020;
    private const ushort HighEntVa = 0x0020;
    private const uint DefImageBase = 0x00400000;
    private const uint CodeSectFlag = 0x00000020;
    private const uint InitDataFlag = 0x00000040;

    public static void Patch(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: true);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        PEHeaders headers = reader.PEHeaders;
        PEHeader peHeader = headers.PEHeader ?? throw new InvalidOperationException("The PE header is missing.");
        CheckSupport(headers, peHeader);
        RewriteHdr(bytes, headers, FindBod(bytes, headers, peHeader.BaseOfCode));
        NormCorFlags(bytes, headers);
        File.WriteAllBytes(path, bytes);
    }

    private static void CheckSupport(PEHeaders headers, PEHeader peHeader)
    {
        if (peHeader.Magic != PEMagic.PE32Plus)
        {
            throw new InvalidOperationException("Only PE32+ managed assemblies can be patched.");
        }

        if (headers.CorHeader is null)
        {
            throw new InvalidOperationException("A CLI header is required for patching.");
        }
    }

    private static void RewriteHdr(byte[] bytes, PEHeaders headers, uint baseOfData)
    {
        int optStart = headers.PEHeaderStartOffset;
        int coffStart = optStart - CoffHdrSize;
        WriteUInt16(bytes, coffStart + MachOff, I386Machine);
        WriteUInt16(bytes, coffStart + OptSizeOff, Pe32OptSize);
        PatchChars(bytes, coffStart + CharsOff);
        WriteOptHdr(bytes, optStart, baseOfData);
        ShiftSects(bytes, headers, optStart);
    }

    private static void PatchChars(byte[] bytes, int offset)
    {
        ushort characteristics = ReadUInt16(bytes, offset);
        characteristics = (ushort)((characteristics | Img32BitMach) & ~BigAddrAware);
        WriteUInt16(bytes, offset, characteristics);
    }

    private static void WriteOptHdr(byte[] bytes, int offset, uint baseOfData)
    {
        byte[] header = BuildPe32Opt(bytes, offset, baseOfData);
        header.CopyTo(bytes.AsSpan(offset, header.Length));
    }

    private static byte[] BuildPe32Opt(byte[] bytes, int offset, uint baseOfData)
    {
        byte[] header = new byte[Pe32OptSize];
        bytes.AsSpan(offset, 24).CopyTo(header);
        WriteUInt16(header, 0, Pe32Magic);
        WriteUInt32(header, 24, baseOfData);
        WriteUInt32(header, 28, DefImageBase);
        bytes.AsSpan(offset + 32, 40).CopyTo(header.AsSpan(32, 40));
        WriteUInt16(header, 70, (ushort)(ReadUInt16(header, 70) & ~HighEntVa));
        WriteUInt32(header, 72, checked((uint)ReadUInt64(bytes, offset + 72)));
        WriteUInt32(header, 76, checked((uint)ReadUInt64(bytes, offset + 80)));
        WriteUInt32(header, 80, checked((uint)ReadUInt64(bytes, offset + 88)));
        WriteUInt32(header, 84, checked((uint)ReadUInt64(bytes, offset + 96)));
        bytes.AsSpan(offset + 104, 8).CopyTo(header.AsSpan(88, 8));
        bytes.AsSpan(offset + 112, 128).CopyTo(header.AsSpan(96, 128));
        return header;
    }

    private static void ShiftSects(byte[] bytes, PEHeaders headers, int optStart)
    {
        short oldOptSize = headers.CoffHeader.SizeOfOptionalHeader;
        if (oldOptSize == Pe32OptSize)
        {
            return;
        }

        int oldStart = optStart + oldOptSize;
        int newStart = optStart + Pe32OptSize;
        int tableLen = headers.CoffHeader.NumberOfSections * SectHdrSize;
        Array.Copy(bytes, oldStart, bytes, newStart, tableLen);
        bytes.AsSpan(newStart + tableLen, oldStart - newStart).Clear();
    }

    private static void NormCorFlags(byte[] bytes, PEHeaders headers)
    {
        int flagsOffset = headers.CorHeaderStartOffset + 16;
        int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(flagsOffset, sizeof(int)));
        flags &= ~(int)(CorFlags.Requires32Bit | CorFlags.Prefers32Bit);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(flagsOffset, sizeof(int)), flags);
    }

    private static uint FindBod(byte[] bytes, PEHeaders headers, int baseOfCode)
    {
        int tableStart = headers.PEHeaderStartOffset + headers.CoffHeader.SizeOfOptionalHeader;
        short sectionCount = headers.CoffHeader.NumberOfSections;
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = tableStart + (index * SectHdrSize);
            uint virtualAddress = ReadUInt32(bytes, offset + SectVaOff);
            uint size = ReadUInt32(bytes, offset + SectSzOff);
            uint characteristics = ReadUInt32(bytes, offset + SectCharsOff);
            if (size == 0 || virtualAddress == baseOfCode || (characteristics & CodeSectFlag) != 0)
            {
                continue;
            }

            if ((characteristics & InitDataFlag) != 0)
            {
                return virtualAddress;
            }
        }

        return (uint)baseOfCode;
    }

    private static ulong ReadUInt64(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)));
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
    }
}
