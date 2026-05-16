using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Plan;

namespace PeFix.Patch;

public static class RedirPatch
{
    private const int UInt16ByteLength = 2;
    private const int VersionByteLength = 8;
    private const int MinorOffset = 2;
    private const int BuildOffset = 4;
    private const int RevisionOffset = 6;

    public static RedirResult Redir(string path, RedirOptions options)
    {
        ValidateEncodableVersion(options.ToVersion);
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        MetadataReader reader = peReader.GetMetadataReader();
        int tableHeapOffset = MetadataTableOffset(origBytes, peReader);
        int[] matches = FindMatches(reader, options);

        if (matches.Length == 0)
            return new RedirResult(fullPath, null, null, options.DryRun, 0);

        if (options.DryRun)
            return new RedirResult(fullPath, null, null, true, matches.Length);

        RedirWork work = BuildPatch(origBytes, tableHeapOffset, matches, options.ToVersion);
        string? backupPath = options.Backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteVerifiedAtomic(fullPath, work.Patched, tmpPath =>
        {
            VerifyWrittenAssemblyRefs(tmpPath, options, matches);
            Validator.Validate(tmpPath);
        });
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, work.Patched, PeUtils.ReadMvid(work.Patched)),
            work.Ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);

        return new RedirResult(fullPath, backupPath, planPath, false, matches.Length);
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
                RedirResult result = Redir(dll, options);
                if (result.RowsPatched > 0) results.Add(result);
            }
            catch (InvalidOperationException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }
        return new RedBatch(fullDir, [.. results], [.. refusals]);
    }

    private static int MetadataTableOffset(byte[] bytes, PEReader peReader)
    {
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("No CLI header -- not a managed assembly.");
        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, metaRva);
        return PeUtils.FindHeap(bytes, metaOffset, "#~");
    }

    private static int[] FindMatches(MetadataReader reader, RedirOptions options)
    {
        List<int> matches = [];
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference assemblyRef = reader.GetAssemblyReference(handle);
            string name = reader.GetString(assemblyRef.Name);
            if (!string.Equals(name, options.Name, StringComparison.Ordinal)) continue;
            if (assemblyRef.Version != options.FromVersion) continue;
            matches.Add(MetadataTokens.GetRowNumber(handle));
        }

        return [.. matches];
    }

    private static RedirWork BuildPatch(byte[] origBytes, int tableHeapOffset, int[] matches, Version toVersion)
    {
        byte[] patched = (byte[])origBytes.Clone();
        List<MutationOp> ops = new();

        foreach (int rowIndex in matches)
        {
            int rowOffset = EcmaTables.RowOffset(TableId.AsmRef, origBytes, tableHeapOffset, rowIndex);
            byte[] before = origBytes.AsSpan(rowOffset, VersionByteLength).ToArray();
            WriteVersion(patched, rowOffset, toVersion);
            byte[] after = patched.AsSpan(rowOffset, VersionByteLength).ToArray();
            ops.Add(new MutationOp(
                "redir.version",
                new PlanTarget("asmref", Table: "AssemblyRef", Row: rowIndex, Offset: rowOffset),
                HexUtils.Hex(before),
                HexUtils.Hex(after)));
        }

        return new RedirWork(patched, [.. ops]);
    }

    private static void WriteVersion(byte[] bytes, int rowOffset, Version version)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(rowOffset, UInt16ByteLength), (ushort)version.Major);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(rowOffset + MinorOffset, UInt16ByteLength), (ushort)version.Minor);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(rowOffset + BuildOffset, UInt16ByteLength), (ushort)version.Build);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(rowOffset + RevisionOffset, UInt16ByteLength), (ushort)version.Revision);
    }

    private static void VerifyWrittenAssemblyRefs(string path, RedirOptions options, int[] rows)
    {
        byte[] writtenBytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(writtenBytes, writable: false);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        HashSet<int> expectedRows = [.. rows];
        int verifiedRows = 0;

        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            int rowIndex = MetadataTokens.GetRowNumber(handle);
            if (!expectedRows.Contains(rowIndex)) continue;
            VerifyAssemblyRefRow(reader, handle, new AssemblyRefVerifyContext(path, options, rowIndex));
            verifiedRows++;
        }

        if (verifiedRows != rows.Length)
            throw new InvalidOperationException(
                $"Post-write verification failed for {path}: verified {verifiedRows} of {rows.Length} AssemblyRef row(s).");

    }

    private static void VerifyAssemblyRefRow(
        MetadataReader reader,
        AssemblyReferenceHandle handle,
        AssemblyRefVerifyContext context)
    {
        AssemblyReference assemblyRef = reader.GetAssemblyReference(handle);
        string name = reader.GetString(assemblyRef.Name);
        if (!string.Equals(name, context.Options.Name, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Post-write verification failed for {context.Path}: AssemblyRef row {context.RowIndex} is '{name}', expected '{context.Options.Name}'.");

        if (assemblyRef.Version != context.Options.ToVersion)
            throw new InvalidOperationException(
                $"Post-write verification failed for {context.Path}: AssemblyRef row {context.RowIndex} stayed at {assemblyRef.Version}; expected {context.Options.FromVersion} -> {context.Options.ToVersion}.");
    }

    private static void ValidateEncodableVersion(Version version)
    {
        if (version.Major < 0
            || version.Minor < 0
            || version.Build < 0
            || version.Revision < 0
            || version.Major > ushort.MaxValue
            || version.Minor > ushort.MaxValue
            || version.Build > ushort.MaxValue
            || version.Revision > ushort.MaxValue)
            throw new InvalidOperationException("Target version must have four numeric fields, each in [0..65535].");
    }

    private readonly record struct RedirWork(byte[] Patched, MutationOp[] Ops);

    private readonly record struct AssemblyRefVerifyContext(string Path, RedirOptions Options, int RowIndex);
}
