using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace PeFix.Patch;

internal static class PeUtils
{
    internal static string ReadMvid(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        if (pe.PEHeaders.CorHeader is null) return "";
        MetadataReader reader = pe.GetMetadataReader();
        return reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString();
    }

    internal static int RvaToOffset(PEHeaders headers, int rva)
    {
        foreach (SectionHeader section in headers.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData)
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }
        throw new InvalidOperationException($"RVA 0x{rva:X8} not found in any PE section.");
    }

    internal static int FindHeap(byte[] bytes, int metaOffset, string heapName)
    {
        int pos = metaOffset + 12;
        int verLen = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos));
        pos += 4 + verLen;
        pos += 2; // flags
        int numStreams = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pos)); pos += 2;
        for (int i = 0; i < numStreams; i++)
        {
            int streamOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos)); pos += 4;
            pos += 4; // size
            int nameStart = pos;
            while (bytes[pos] != 0) pos++;
            string name = Encoding.ASCII.GetString(bytes, nameStart, pos - nameStart);
            pos = (pos + 4) & ~3;
            if (string.Equals(name, heapName, StringComparison.Ordinal)) return metaOffset + streamOffset;
        }
        throw new InvalidOperationException($"Metadata heap '{heapName}' not found.");
    }

    internal static int BlobPrefixLen(int length)
    {
        if (length <= 0x7F) return 1;
        if (length <= 0x3FFF) return 2;
        return 4;
    }

    internal static string Backup(string path)
    {
        string backupPath = path + ".bak";
        try { File.Copy(path, backupPath, overwrite: false); }
        catch (IOException) when (File.Exists(backupPath))
        {
            throw new IOException($"Backup file {backupPath} already exists. Remove it or run with --no-backup.");
        }
        return backupPath;
    }

    internal static void WriteVerifiedAtomic(string path, byte[] bytes, Action<string> verify)
    {
        string tmp = $"{path}.tmp.{Environment.ProcessId}";
        try
        {
            File.WriteAllBytes(tmp, bytes);
            verify(tmp);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
    }

}
