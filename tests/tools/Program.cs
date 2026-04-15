using System.Buffers.Binary;
using System.IO;
using System.Reflection.PortableExecutable;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: Deriver <transform> <source-or-placeholder> <target>");
    Console.Error.WriteLine("transforms: mixed-mode | native-pe | corrupt | empty | r2r-marker | webcil | single-file-bundle | conflict");
    return 2;
}

string transform = args[0];
string source = args[1];
string target = args[2];
const int CorFlagOff = 16;
const int CorDirIdx = 14;
const int DirSize = 8;

Directory.CreateDirectory(Path.GetDirectoryName(target)!);

switch (transform)
{
    case "mixed-mode":
        WriteMixed(source, target);
        break;
    case "native-pe":
        WriteNative(source, target);
        break;
    case "corrupt":
        WriteCorrupt(source, target);
        break;
    case "empty":
        File.WriteAllBytes(target, []);
        break;
    case "r2r-marker":
        PeWrites.WriteR2R(source, target);
        break;
    case "webcil":
        WriteWebcil(target);
        break;
    case "single-file-bundle":
        WriteBundle(source, target);
        break;
    case "conflict":
        PeWrites.WriteConf(target);
        break;
    default:
        Console.Error.WriteLine($"unknown transform: {transform}");
        return 2;
}

return 0;

void WriteMixed(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    using var stream = new MemoryStream(bytes, writable: true);
    using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    int offset = reader.PEHeaders.CorHeaderStartOffset + CorFlagOff;
    int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), flags & ~(int)CorFlags.ILOnly);
    File.WriteAllBytes(targetPath, bytes);
}

void WriteNative(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    using var stream = new MemoryStream(bytes, writable: true);
    using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    int dirsOff = reader.PEHeaders.PEHeaderStartOffset
        + (reader.PEHeaders.PEHeader!.Magic == PEMagic.PE32 ? 96 : 112);
    int corDirOff = dirsOff + (CorDirIdx * DirSize);
    bytes.AsSpan(corDirOff, 8).Clear();
    File.WriteAllBytes(targetPath, bytes);
}

void WriteCorrupt(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    int cutLen = Math.Min(100, bytes.Length);
    File.WriteAllBytes(targetPath, bytes.AsSpan(0, cutLen).ToArray());
}

void WriteWebcil(string targetPath)
{
    File.WriteAllBytes(targetPath, [
        0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00,
        0x57, 0x62, 0x49, 0x4C
    ]);
}

void WriteBundle(string sourcePath, string targetPath)
{
    ReadOnlySpan<byte> sig = [
        0x8b, 0x1c, 0xcd, 0x0d, 0xfe, 0xfe, 0xfe, 0xfe,
        0x13, 0x12, 0x13, 0x13, 0x11, 0x06, 0x0b, 0x06
    ];
    byte[] bytes = File.ReadAllBytes(sourcePath);
    byte[] result = new byte[bytes.Length + sig.Length];
    bytes.CopyTo(result, 0);
    sig.CopyTo(result.AsSpan(bytes.Length));
    File.WriteAllBytes(targetPath, result);
}
