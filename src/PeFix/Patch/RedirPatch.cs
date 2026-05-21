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
        return Redir(path, options, VerifyWrittenAssemblyRefs);
    }

    internal static RedirResult Redir(
        string path,
        RedirOptions options,
        Action<string, RedirOptions, int[]> verify)
    {
        ValidateEncodableVersion(options.ToVersion);
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        int tableHeapOffset = MetadataTableOffset(origBytes, peReader);
        MetadataReader reader = peReader.GetMetadataReader();
        int[] matches = FindMatches(reader, options);

        if (matches.Length == 0)
            return new RedirResult(fullPath, null, null, options.DryRun, []);

        RedirWork work = BuildPatch(new RedirPatchRequest
        {
            Original = origBytes,
            TableHeapOffset = tableHeapOffset,
            Matches = matches,
            ToVersion = options.ToVersion
        });

        if (options.DryRun)
            return new RedirResult(fullPath, null, null, true, work.Ops);

        VerifiedWriteResult write = VerifiedWrite.Apply(new VerifiedWrite.Request
        {
            Path = fullPath,
            Original = origBytes,
            Patched = work.Patched,
            Ops = work.Ops,
            Backup = options.Backup,
            Verify = tmpPath => verify(tmpPath, options, matches)
        });

        return new RedirResult(fullPath, write.BackupPath, write.PlanPath, false, work.Ops);
    }

    public static RedBatch RedirDir(string dir, RedirOptions options)
    {
        ValidateEncodableVersion(options.ToVersion);
        string fullDir = Path.GetFullPath(dir);
        List<RedirCandidate> candidates = [];
        List<Refusal> refusals = [];
        foreach (string dll in Directory.EnumerateFiles(fullDir, "*.dll"))
        {
            try
            {
                RedirCandidate? candidate = BuildCandidate(dll, options);
                if (candidate is { } hit) candidates.Add(hit);
            }
            catch (RefusalException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }

        if (options.DryRun || refusals.Count > 0)
            return new RedBatch(fullDir, [.. candidates.Select(ToDryRun)], [.. refusals]);

        List<RedirResult> results = [];
        VerifiedWriteResult[] writes = VerifiedWrite.ApplyBatch([.. candidates.Select(candidate => CreateRequest(candidate, options, VerifyWrittenAssemblyRefs))]);
        for (int index = 0; index < candidates.Count; index++)
        {
            RedirCandidate candidate = candidates[index];
            VerifiedWriteResult write = writes[index];
            results.Add(new RedirResult(candidate.Path, write.BackupPath, write.PlanPath, false, candidate.Ops));
        }

        return new RedBatch(fullDir, [.. results], [.. refusals]);
    }

    private static RedirCandidate? BuildCandidate(string path, RedirOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);

        using var readStream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(readStream);
        int tableHeapOffset = MetadataTableOffset(origBytes, peReader);
        MetadataReader reader = peReader.GetMetadataReader();
        int[] matches = FindMatches(reader, options);

        if (matches.Length == 0)
            return null;

        RedirWork work = BuildPatch(new RedirPatchRequest
        {
            Original = origBytes,
            TableHeapOffset = tableHeapOffset,
            Matches = matches,
            ToVersion = options.ToVersion
        });
        if (options.DryRun)
        {
            return new RedirCandidate
            {
                Path = fullPath,
                Original = origBytes,
                Patched = origBytes,
                Ops = work.Ops,
                Matches = matches
            };
        }

        VerifiedWrite.Preflight(fullPath, options.Backup);

        return new RedirCandidate
        {
            Path = fullPath,
            Original = origBytes,
            Patched = work.Patched,
            Ops = work.Ops,
            Matches = matches
        };
    }

    private static RedirResult ToDryRun(RedirCandidate candidate)
    {
        return new RedirResult(candidate.Path, null, null, true, candidate.Ops);
    }

    private static VerifiedWrite.Request CreateRequest(
        RedirCandidate candidate,
        RedirOptions options,
        Action<string, RedirOptions, int[]> verify)
    {
        return new VerifiedWrite.Request
        {
            Path = candidate.Path,
            Original = candidate.Original,
            Patched = candidate.Patched,
            Ops = candidate.Ops,
            Backup = options.Backup,
            Verify = tmpPath => verify(tmpPath, options, candidate.Matches)
        };
    }

    private static int MetadataTableOffset(byte[] bytes, PEReader peReader)
    {
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new RefusalException("No CLI header -- not a managed assembly.");
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

    private static RedirWork BuildPatch(RedirPatchRequest request)
    {
        byte[] patched = (byte[])request.Original.Clone();
        List<MutationOp> ops = new();

        foreach (int rowIndex in request.Matches)
        {
            int rowOffset = EcmaTables.RowOffset(new EcmaTables.RowOffsetRequest
            {
                TableId = TableId.AsmRef,
                Bytes = request.Original,
                TableHeapOffset = request.TableHeapOffset,
                RowIndex = rowIndex
            });
            byte[] before = request.Original.AsSpan(rowOffset, VersionByteLength).ToArray();
            WriteVersion(patched, rowOffset, request.ToVersion);
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

    private sealed record RedirPatchRequest
    {
        public required byte[] Original { get; init; }
        public required int TableHeapOffset { get; init; }
        public required int[] Matches { get; init; }
        public required Version ToVersion { get; init; }
    }

    private sealed record RedirCandidate
    {
        public required string Path { get; init; }
        public required byte[] Original { get; init; }
        public required byte[] Patched { get; init; }
        public required MutationOp[] Ops { get; init; }
        public required int[] Matches { get; init; }
    }

    private readonly record struct AssemblyRefVerifyContext(string Path, RedirOptions Options, int RowIndex);
}
