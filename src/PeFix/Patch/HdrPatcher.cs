using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace PeFix.Patch;

internal static class HdrPatcher
{
    private const int CoffHeaderSize = 20;
    private const int MachineOffset = 0;
    private const int OptionalHeaderSizeOffset = 16;
    private const int CharacteristicsOffset = 18;
    private const int SectionHeaderSize = 40;
    private const int SectionVirtualAddressOffset = 12;
    private const int SectionCharacteristicsOffset = 36;
    private const int SectionSizeOffset = 8;
    private const int Pe32OptionalHeaderSize = 224;
    private const ushort Pe32Magic = 0x10B;
    private const ushort I386Machine = 0x14C;
    private const ushort Image32BitMachine = 0x0100;
    private const ushort BigAddrAware = 0x0020;
    private const ushort HighEntropyVa = 0x0020;
    private const uint DefaultImageBase = 0x00400000;
    private const uint CodeSectionFlag = 0x00000020;
    private const uint InitializedDataFlag = 0x00000040;

    public static byte[] Patch(byte[] original)
    {
        byte[] patched = (byte[])original.Clone();
        using var stream = new MemoryStream(patched, writable: true);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        PEHeaders headers = reader.PEHeaders;
        PEHeader peHeader = headers.PEHeader ?? throw new InvalidOperationException("The PE header is missing.");
        CheckSupport(headers, peHeader);
        RewriteHeader(patched, headers, FindBaseOfData(patched, headers, peHeader.BaseOfCode));
        NormalizeCorFlags(patched, headers);
        return patched;
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

    private static void RewriteHeader(byte[] bytes, PEHeaders headers, uint baseOfData)
    {
        int optionalHeaderStart = headers.PEHeaderStartOffset;
        int coffStart = optionalHeaderStart - CoffHeaderSize;
        WriteUInt16(bytes, coffStart + MachineOffset, I386Machine);
        WriteUInt16(bytes, coffStart + OptionalHeaderSizeOffset, Pe32OptionalHeaderSize);
        PatchCharacteristics(bytes, coffStart + CharacteristicsOffset);
        WriteOptionalHeader(bytes, optionalHeaderStart, baseOfData);
        ShiftSections(bytes, headers, optionalHeaderStart);
    }

    private static void PatchCharacteristics(byte[] bytes, int offset)
    {
        ushort characteristics = ReadUInt16(bytes, offset);
        characteristics = (ushort)((characteristics | Image32BitMachine) & ~BigAddrAware);
        WriteUInt16(bytes, offset, characteristics);
    }

    private static void WriteOptionalHeader(byte[] bytes, int offset, uint baseOfData)
    {
        byte[] header = BuildPe32OptionalHeader(bytes, offset, baseOfData);
        header.CopyTo(bytes.AsSpan(offset, header.Length));
    }

    private static byte[] BuildPe32OptionalHeader(byte[] bytes, int offset, uint baseOfData)
    {
        byte[] header = new byte[Pe32OptionalHeaderSize];
        bytes.AsSpan(offset, 24).CopyTo(header);
        WriteUInt16(header, 0, Pe32Magic);
        WriteUInt32(header, 24, baseOfData);
        WriteUInt32(header, 28, DefaultImageBase);
        bytes.AsSpan(offset + 32, 40).CopyTo(header.AsSpan(32, 40));
        WriteUInt16(header, 70, (ushort)(ReadUInt16(header, 70) & ~HighEntropyVa));
        WriteUInt32(header, 72, checked((uint)ReadUInt64(bytes, offset + 72)));
        WriteUInt32(header, 76, checked((uint)ReadUInt64(bytes, offset + 80)));
        WriteUInt32(header, 80, checked((uint)ReadUInt64(bytes, offset + 88)));
        WriteUInt32(header, 84, checked((uint)ReadUInt64(bytes, offset + 96)));
        bytes.AsSpan(offset + 104, 8).CopyTo(header.AsSpan(88, 8));
        bytes.AsSpan(offset + 112, 128).CopyTo(header.AsSpan(96, 128));
        return header;
    }

    private static void ShiftSections(byte[] bytes, PEHeaders headers, int optionalHeaderStart)
    {
        short oldOptionalHeaderSize = headers.CoffHeader.SizeOfOptionalHeader;
        if (oldOptionalHeaderSize == Pe32OptionalHeaderSize)
        {
            return;
        }

        int oldStart = optionalHeaderStart + oldOptionalHeaderSize;
        int newStart = optionalHeaderStart + Pe32OptionalHeaderSize;
        int tableLen = headers.CoffHeader.NumberOfSections * SectionHeaderSize;
        Array.Copy(bytes, oldStart, bytes, newStart, tableLen);
        bytes.AsSpan(newStart + tableLen, oldStart - newStart).Clear();
    }

    private static void NormalizeCorFlags(byte[] bytes, PEHeaders headers)
    {
        int flagsOffset = headers.CorHeaderStartOffset + 16;
        int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(flagsOffset, sizeof(int)));
        flags &= ~(int)(CorFlags.Requires32Bit | CorFlags.Prefers32Bit);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(flagsOffset, sizeof(int)), flags);
    }

    private static uint FindBaseOfData(byte[] bytes, PEHeaders headers, int baseOfCode)
    {
        int tableStart = headers.PEHeaderStartOffset + headers.CoffHeader.SizeOfOptionalHeader;
        short sectionCount = headers.CoffHeader.NumberOfSections;
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = tableStart + (index * SectionHeaderSize);
            uint virtualAddress = ReadUInt32(bytes, offset + SectionVirtualAddressOffset);
            uint size = ReadUInt32(bytes, offset + SectionSizeOffset);
            uint characteristics = ReadUInt32(bytes, offset + SectionCharacteristicsOffset);
            if (size == 0 || virtualAddress == baseOfCode || (characteristics & CodeSectionFlag) != 0)
            {
                continue;
            }

            if ((characteristics & InitializedDataFlag) != 0)
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
