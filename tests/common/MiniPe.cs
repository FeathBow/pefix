using System.Buffers.Binary;

namespace PeFix.Tests;

internal static class MiniPe
{
    internal static void Write(string path, byte[] meta)
    {
        const int fileAlign = 0x200;
        const int sectAlign = 0x2000;
        const int imageBase = 0x10000000;
        const int sectRva = sectAlign;
        const int cliSize = 0x48;
        const int headerSize = fileAlign;

        int rawSize = Align(cliSize + meta.Length, fileAlign);
        int imageSize = Align(sectRva + rawSize, sectAlign);
        int metaRva = sectRva + cliSize;

        byte[] pe = new byte[headerSize + rawSize];
        Span<byte> b = pe.AsSpan();

        ReadOnlySpan<byte> dos = [
            0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
            0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00,
        ];
        dos.CopyTo(b);
        int pos = 0x80;
        W32(b, ref pos, 0x00004550);
        W16(b, ref pos, 0x014C);
        W16(b, ref pos, 1);
        W32(b, ref pos, 0); W32(b, ref pos, 0); W32(b, ref pos, 0);
        W16(b, ref pos, 0xE0);
        W16(b, ref pos, 0x2102);
        W16(b, ref pos, 0x010B);
        b[pos++] = 6; b[pos++] = 0;
        W32(b, ref pos, 0); W32(b, ref pos, (uint)rawSize); W32(b, ref pos, 0); W32(b, ref pos, 0);
        W32(b, ref pos, (uint)sectRva); W32(b, ref pos, 0);
        W32(b, ref pos, (uint)imageBase);
        W32(b, ref pos, (uint)sectAlign); W32(b, ref pos, (uint)fileAlign);
        W16(b, ref pos, 4); W16(b, ref pos, 0); W16(b, ref pos, 0); W16(b, ref pos, 0);
        W16(b, ref pos, 4); W16(b, ref pos, 0);
        W32(b, ref pos, 0); W32(b, ref pos, (uint)imageSize); W32(b, ref pos, (uint)headerSize);
        W32(b, ref pos, 0); W16(b, ref pos, 3); W16(b, ref pos, 0);
        W32(b, ref pos, 0x100000); W32(b, ref pos, 0x1000);
        W32(b, ref pos, 0x100000); W32(b, ref pos, 0x1000);
        W32(b, ref pos, 0); W32(b, ref pos, 16);
        for (int i = 0; i < 16; i++)
        {
            if (i == 14) { W32(b, ref pos, (uint)sectRva); W32(b, ref pos, (uint)cliSize); }
            else { W32(b, ref pos, 0); W32(b, ref pos, 0); }
        }
        ".text\0\0\0"u8.CopyTo(b[pos..]); pos += 8;
        W32(b, ref pos, (uint)(cliSize + meta.Length));
        W32(b, ref pos, (uint)sectRva);
        W32(b, ref pos, (uint)rawSize);
        W32(b, ref pos, (uint)headerSize);
        W32(b, ref pos, 0); W32(b, ref pos, 0);
        W16(b, ref pos, 0); W16(b, ref pos, 0);
        W32(b, ref pos, 0x60000020);

        Span<byte> cli = pe.AsSpan(headerSize, cliSize);
        int cp = 0;
        W32(cli, ref cp, (uint)cliSize);
        W16(cli, ref cp, 2); W16(cli, ref cp, 5);
        W32(cli, ref cp, (uint)metaRva); W32(cli, ref cp, (uint)meta.Length);
        W32(cli, ref cp, 1); W32(cli, ref cp, 0);

        meta.CopyTo(pe.AsSpan(headerSize + cliSize));
        File.WriteAllBytes(path, pe);
    }

    private static int Align(int v, int a) => (v + a - 1) & ~(a - 1);
    private static void W16(Span<byte> b, ref int p, ushort v) { BinaryPrimitives.WriteUInt16LittleEndian(b[p..], v); p += 2; }
    private static void W32(Span<byte> b, ref int p, uint v) { BinaryPrimitives.WriteUInt32LittleEndian(b[p..], v); p += 4; }
}
