using System;
using System.Buffers.Binary;
using System.IO;
using System.Reflection.PortableExecutable;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: FixtureDeriver <transform> <source-or-placeholder> <target>");
    Console.Error.WriteLine("transforms: mixed-mode | native-pe | corrupt | empty");
    return 2;
}

string transform = args[0];
string source = args[1];
string target = args[2];

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
    default:
        Console.Error.WriteLine($"unknown transform: {transform}");
        return 2;
}

return 0;

static void WriteMixed(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    using var stream = new MemoryStream(bytes, writable: true);
    using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    int offset = reader.PEHeaders.CorHeaderStartOffset + 16;
    int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), flags & ~(int)CorFlags.ILOnly);
    File.WriteAllBytes(targetPath, bytes);
}

static void WriteNative(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    using var stream = new MemoryStream(bytes, writable: true);
    using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    int dataDirectoriesOffset = reader.PEHeaders.PEHeaderStartOffset
        + (reader.PEHeaders.PEHeader!.Magic == PEMagic.PE32 ? 96 : 112);
    int corHeaderDirectoryOffset = dataDirectoriesOffset + (14 * 8);
    bytes.AsSpan(corHeaderDirectoryOffset, 8).Clear();
    File.WriteAllBytes(targetPath, bytes);
}

static void WriteCorrupt(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);
    int truncatedLength = Math.Min(100, bytes.Length);
    File.WriteAllBytes(targetPath, bytes.AsSpan(0, truncatedLength).ToArray());
}
