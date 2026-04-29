using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Plan;

namespace PeFix.Patch;

public static class RedirPatch
{
    private const int U16Len = 2;
    private const int VerLen = 8;
    private const int MinOfs = 2;
    private const int BldOfs = 4;
    private const int RevOfs = 6;

    public static RedirResult Redir(string path, RedirOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("No CLI header -- not a managed assembly.");
        MetadataReader reader = peReader.GetMetadataReader();

        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, metaRva);
        int tildeOfs = PeUtils.FindHeap(origBytes, metaOffset, "#~");

        List<int> matches = [];
        foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
        {
            AssemblyReference ar = reader.GetAssemblyReference(h);
            string name = reader.GetString(ar.Name);
            if (!string.Equals(name, options.Name, StringComparison.Ordinal)) continue;
            if (ar.Version != options.FromVersion) continue;
            matches.Add(MetadataTokens.GetRowNumber(h));
        }

        if (matches.Count == 0)
            return new RedirResult(fullPath, null, null, options.DryRun, 0);

        if (options.DryRun)
            return new RedirResult(fullPath, null, null, true, matches.Count);

        byte[] patched = (byte[])origBytes.Clone();
        List<MutationOp> ops = new();

        foreach (int rowIdx in matches)
        {
            int rowOffset = EcmaTables.RowOffset(TableId.AsmRef, origBytes, tildeOfs, rowIdx);
            byte[] before = origBytes.AsSpan(rowOffset, VerLen).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(rowOffset, U16Len), (ushort)options.ToVersion.Major);
            BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(rowOffset + MinOfs, U16Len), (ushort)options.ToVersion.Minor);
            BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(rowOffset + BldOfs, U16Len), (ushort)options.ToVersion.Build);
            BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(rowOffset + RevOfs, U16Len), (ushort)options.ToVersion.Revision);
            byte[] after = patched.AsSpan(rowOffset, VerLen).ToArray();
            ops.Add(new MutationOp(
                "redir.version",
                new PlanTarget("asmref", Table: "AssemblyRef", Row: rowIdx, Offset: rowOffset),
                HexUtils.Hex(before),
                HexUtils.Hex(after)));
        }

        string? backupPath = options.Backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteAtomic(fullPath, patched);
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, patched, PeUtils.ReadMvid(patched)),
            ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);

        return new RedirResult(fullPath, backupPath, planPath, false, matches.Count);
    }

    public static RedBatch RedirDir(string dir, RedirOptions options)
    {
        string fullDir = Path.GetFullPath(dir);
        List<RedirResult> results = [];
        List<Refusal> refusals = [];
        foreach (string dll in Directory.EnumerateFiles(fullDir, "*.dll"))
        {
            try
            {
                RedirResult r = Redir(dll, options);
                if (r.RowsPatched > 0) results.Add(r);
            }
            catch (InvalidOperationException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }
        return new RedBatch(fullDir, [.. results], [.. refusals]);
    }

}
