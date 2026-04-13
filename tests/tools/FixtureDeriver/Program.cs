using System;
using System.Buffers.Binary;
using System.IO;
using System.Reflection.PortableExecutable;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: FixtureDeriver <transform> <source-or-placeholder> <target>");
    Console.Error.WriteLine("transforms: mixed-mode | native-pe | corrupt | empty | r2r-marker | webcil | single-file-bundle");
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
    case "r2r-marker":
        WriteR2R(source, target);
        break;
    case "webcil":
        WriteWebcil(target);
        break;
    case "single-file-bundle":
        WriteSingleFileBundle(source, target);
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

static void WriteWebcil(string targetPath)
{
    // WebAssembly magic + version (8 bytes). First 4 mirror PeAnalyzer.WasmMagic.
    File.WriteAllBytes(targetPath, [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00]);
}

static void WriteSingleFileBundle(string sourcePath, string targetPath)
{
    // .NET single-file bundle footer (16 bytes). Mirrors PeAnalyzer.BundleSig.
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

static void WriteR2R(string sourcePath, string targetPath)
{
    byte[] bytes = File.ReadAllBytes(sourcePath);

    using var readStream = new MemoryStream(bytes, writable: false);
    using var reader = new PEReader(readStream, PEStreamOptions.LeaveOpen);

    PEHeaders headers = reader.PEHeaders;

    // CorHeader layout (ECMA-335): ManagedNativeHeader directory is at byte offset 64.
    int corHeaderOffset = headers.CorHeaderStartOffset;
    int managedNativeHeaderDirOffset = corHeaderOffset + 64;

    // R2R stub: signature "RTR\0" + major=1 + minor=0.
    byte[] r2rStub = [0x52, 0x54, 0x52, 0x00, 0x01, 0x00, 0x00, 0x00];

    // PEHeaderStartOffset points to the optional header (= COFF start + 20).
    // SizeOfOptionalHeader is in COFF header at offset 16 = PEHeaderStartOffset - 4.
    int peStart = headers.PEHeaderStartOffset;
    ushort optHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(peStart - 4, 2));
    int sectionTableOffset = peStart + optHeaderSize;

    // Find a section with raw padding >= 8 bytes to embed the stub.
    // Expand VirtualSize in the section header so GetSectionData() includes the stub region.
    int bestSectionIndex = -1;
    int bestPadding = 0;
    for (int i = 0; i < headers.SectionHeaders.Length; i++)
    {
        SectionHeader s = headers.SectionHeaders[i];
        int pad = s.SizeOfRawData - s.VirtualSize;
        if (pad >= 8 && pad > bestPadding)
        {
            bestPadding = pad;
            bestSectionIndex = i;
        }
    }

    if (bestSectionIndex < 0)
    {
        throw new InvalidOperationException("No section has >= 8 bytes of raw padding to embed the R2R stub.");
    }

    SectionHeader bestSection = headers.SectionHeaders[bestSectionIndex];

    // Place stub at the raw byte immediately after VirtualSize content.
    int stubFileOffset = (int)bestSection.PointerToRawData + bestSection.VirtualSize;
    int stubRva = (int)bestSection.VirtualAddress + bestSection.VirtualSize;

    // Write the stub into the raw padding area.
    r2rStub.CopyTo(bytes.AsSpan(stubFileOffset));

    // Expand VirtualSize so GetSectionData() maps the stub bytes.
    // VirtualSize field is at section header offset + 8.
    int sectionHeaderOffset = sectionTableOffset + bestSectionIndex * 40;
    int vSizeFieldOffset = sectionHeaderOffset + 8;
    int newVirtualSize = bestSection.VirtualSize + r2rStub.Length;
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(vSizeFieldOffset, 4), newVirtualSize);

    // Patch CorHeader ManagedNativeHeaderDirectory: RVA then Size.
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(managedNativeHeaderDirOffset, 4), stubRva);
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(managedNativeHeaderDirOffset + 4, 4), r2rStub.Length);

    File.WriteAllBytes(targetPath, bytes);
}
