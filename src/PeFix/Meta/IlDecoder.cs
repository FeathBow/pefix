using System.Buffers.Binary;
using System.Collections.Immutable;

namespace PeFix.Meta;

internal static class IlDecoder
{
    internal const int Call = 0x28;
    internal const int Callvirt = 0x6F;
    internal const int Ldstr = 0x72;
    private const int Prefix = 0xFE;
    private const int Switch = 0x45;

    public static DecodeResult Decode(ImmutableArray<byte> content)
    {
        ReadOnlySpan<byte> il = content.AsSpan();
        var instructions = new List<IlInstr>();
        int offset = 0;
        while (offset < il.Length)
        {
            if (!TryReadInstruction(il, ref offset, out IlInstr instruction))
                return new DecodeResult([], Desynced: true);

            instructions.Add(instruction);
        }

        return new DecodeResult([.. instructions], Desynced: false);
    }

    private static bool TryReadInstruction(
        ReadOnlySpan<byte> il,
        ref int offset,
        out IlInstr instruction)
    {
        instruction = default;
        int opCode = ReadOpCode(il, ref offset);
        if (opCode < 0 || !TryReadOperand(il, opCode, ref offset, out int operand))
            return false;

        instruction = new IlInstr(opCode, operand);
        return true;
    }

    private static int ReadOpCode(ReadOnlySpan<byte> il, ref int offset)
    {
        if (offset >= il.Length)
            return -1;

        int first = il[offset++];
        if (first != Prefix)
            return first;

        if (offset >= il.Length)
            return -1;

        return (Prefix << 8) | il[offset++];
    }

    private static bool TryReadOperand(
        ReadOnlySpan<byte> il,
        int opCode,
        ref int offset,
        out int operand)
    {
        operand = 0;
        int size = OperandSize(il, opCode, offset);
        if (size < 0 || offset + size > il.Length)
            return false;

        if (size == sizeof(int))
            operand = BinaryPrimitives.ReadInt32LittleEndian(il[offset..]);

        offset += size;
        return true;
    }

    private static int OperandSize(ReadOnlySpan<byte> il, int opCode, int offset)
    {
        if (opCode != Switch)
            return FixedOperandSize(opCode);

        if (offset + sizeof(int) > il.Length)
            return -1;

        int count = BinaryPrimitives.ReadInt32LittleEndian(il[offset..]);
        return count < 0 ? -1 : sizeof(int) + count * sizeof(int);
    }

    private static int FixedOperandSize(int opCode) => opCode switch
    {
        0x0E or 0x0F or 0x10 or 0x11 or 0x12 or 0x13 or 0x1F => 1,
        >= 0x2B and <= 0x37 => 1,
        0xDE or 0xFE12 => 1,
        >= 0xFE09 and <= 0xFE0E => 2,
        0x20 or 0x22 or 0x27 or 0x28 or 0x29 => 4,
        >= 0x38 and <= 0x44 => 4,
        >= 0x6F and <= 0x75 => 4,
        0x79 or >= 0x7B and <= 0x81 => 4,
        0x8C or 0x8D or 0x8F or 0xA3 or 0xA4 or 0xA5 => 4,
        0xC2 or 0xC6 or 0xD0 or 0xDD => 4,
        0xFE06 or 0xFE07 or 0xFE15 or 0xFE16 or 0xFE1C => 4,
        0x21 or 0x23 => 8,
        _ when IsNoOperand(opCode) => 0,
        _ => -1
    };

    private static bool IsNoOperand(int opCode)
    {
        return opCode is >= 0x00 and <= 0x0D
            or >= 0x14 and <= 0x1E
            or 0x25 or 0x26 or 0x2A
            or >= 0x46 and <= 0x6E
            or 0x76 or 0x7A or 0x8E
            or >= 0x82 and <= 0x8B
            or >= 0x90 and <= 0xA2
            or >= 0xB3 and <= 0xBF
            or 0xC3
            or >= 0xD1 and <= 0xDC
            or 0xDF or 0xE0
            or >= 0xFE00 and <= 0xFE05
            or 0xFE0F or 0xFE11 or 0xFE13 or 0xFE14
            or 0xFE17 or 0xFE18 or 0xFE1A or 0xFE1D or 0xFE1E;
    }

}

internal readonly record struct DecodeResult(
    IlInstr[] Instructions,
    bool Desynced);

internal readonly record struct IlInstr(
    int OpCode,
    int Operand);
