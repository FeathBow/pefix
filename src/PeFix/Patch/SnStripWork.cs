using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Meta;
using PeFix.Plan;

namespace PeFix.Patch;

internal static class SnStripWork
{
    private const int CorFlagsOffset = 16;
    private const int UInt32ByteLength = 4;
    private const int HexCharsPerByte = 2;

    internal static SnSelfWork AnalyzeSelf(byte[] bytes, SnStripOpts options)
    {
        return StripSelf(bytes, options);
    }

    internal static SnDependencyWork? BuildDependency(byte[] sourceBytes, IReadOnlyList<string> targetNames)
    {
        return PatchDependencyDll(sourceBytes, targetNames);
    }

    private static SnSelfWork StripSelf(byte[] bytes, SnStripOpts options)
    {
        using var readStream = new MemoryStream(bytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new RefusalException("No CLI header. Not a managed assembly.");
        MetadataReader reader = peReader.GetMetadataReader();

        bool hadIvt = HasSignedIvt(reader);
        if (hadIvt && !options.Force)
            throw new UnsafeException(
                "This assembly uses InternalsVisibleTo with a signed PublicKey. " +
                "Use --force to strip anyway.");

        string asmName = reader.GetString(reader.GetAssemblyDefinition().Name);
        bool isSigned = corHeader.Flags.HasFlag(CorFlags.StrongNameSigned)
            || !reader.GetAssemblyDefinition().PublicKey.IsNil;

        if (!isSigned)
            return new SnSelfWork
            {
                Patched = bytes,
                HadIvt = hadIvt,
                AssemblyName = asmName,
                WasSigned = false,
                Ops = []
            };

        return StripSignedSelf(new SelfStripRequest
        {
            Bytes = bytes,
            Headers = peReader.PEHeaders,
            CorHeader = corHeader,
            Reader = reader,
            HadIvt = hadIvt,
            AssemblyName = asmName
        });
    }

    private static SnSelfWork StripSignedSelf(SelfStripRequest request)
    {
        byte[] patched = (byte[])request.Bytes.Clone();
        List<MutationOp> ops = [];
        var patch = new SelfPatch
        {
            Before = request.Bytes,
            After = patched,
            Headers = request.Headers,
            CorHeader = request.CorHeader,
            Reader = request.Reader,
            BlobOffset = BlobHeapOffset(request.Bytes, request.Headers, request.CorHeader),
            Ops = ops
        };
        ClearStrongNameFlag(patch);
        ClearSignature(patch);
        ClearPublicKey(patch);
        return new SnSelfWork
        {
            Patched = patched,
            HadIvt = request.HadIvt,
            AssemblyName = request.AssemblyName,
            WasSigned = true,
            Ops = ops
        };
    }

    private static int BlobHeapOffset(byte[] bytes, PEHeaders headers, CorHeader corHeader)
    {
        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(headers, metaRva);
        return PeUtils.FindHeap(bytes, metaOffset, "#Blob");
    }

    private static void ClearStrongNameFlag(SelfPatch patch)
    {
        int flagsOffset = patch.Headers.CorHeaderStartOffset + CorFlagsOffset;
        uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(patch.Before.AsSpan(flagsOffset, UInt32ByteLength));
        uint flagsAfter = flagsBefore & ~(uint)CorFlags.StrongNameSigned;
        BinaryPrimitives.WriteUInt32LittleEndian(patch.After.AsSpan(flagsOffset, UInt32ByteLength), flagsAfter);
        patch.Ops.Add(MakeOp(new SnOpTarget
        {
            Kind = "corflags",
            Offset = flagsOffset,
            Before = HexUtils.Hex(BitConverter.GetBytes(flagsBefore)),
            After = HexUtils.Hex(BitConverter.GetBytes(flagsAfter))
        }));
    }

    private static void ClearSignature(SelfPatch patch)
    {
        DirectoryEntry strongNameDirectory = patch.CorHeader.StrongNameSignatureDirectory;
        if (strongNameDirectory.Size == 0) return;

        int strongNameOffset = PeUtils.RvaToOffset(patch.Headers, strongNameDirectory.RelativeVirtualAddress);
        byte[] strongNameBefore = patch.Before.AsSpan(strongNameOffset, strongNameDirectory.Size).ToArray();
        patch.After.AsSpan(strongNameOffset, strongNameDirectory.Size).Clear();
        patch.Ops.Add(MakeOp(new SnOpTarget
        {
            Kind = "snsig",
            Offset = strongNameOffset,
            Before = HexUtils.Hex(strongNameBefore),
            After = Zeros(strongNameDirectory.Size)
        }));
    }

    private static void ClearPublicKey(SelfPatch patch)
    {
        BlobHandle publicKeyHandle = patch.Reader.GetAssemblyDefinition().PublicKey;
        if (publicKeyHandle.IsNil) return;

        byte[] publicKey = patch.Reader.GetBlobBytes(publicKeyHandle);
        if (publicKey.Length == 0) return;

        int heapOffset = MetadataTokens.GetHeapOffset(publicKeyHandle);
        int writeOffset = patch.BlobOffset + heapOffset + PeUtils.BlobPrefixLen(publicKey.Length);
        byte[] publicKeyBefore = patch.Before.AsSpan(writeOffset, publicKey.Length).ToArray();
        patch.After.AsSpan(writeOffset, publicKey.Length).Clear();
        patch.Ops.Add(MakeOp(new SnOpTarget
        {
            Kind = "asm.publickey",
            Offset = writeOffset,
            Before = HexUtils.Hex(publicKeyBefore),
            After = Zeros(publicKey.Length)
        }));
    }

    private static MutationOp MakeOp(SnOpTarget target) =>
        new("snstrip", new PlanTarget(target.Kind, Offset: target.Offset), target.Before, target.After);

    private static string Zeros(int len) => new('0', len * HexCharsPerByte);

    private static SnDependencyWork? PatchDependencyDll(byte[] origBytes, IReadOnlyList<string> targetNames)
    {
        byte[] patched = (byte[])origBytes.Clone();
        using var stream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(stream);
        CorHeader? corHeader = peReader.PEHeaders.CorHeader;
        if (corHeader is null) return null;

        MetadataReader reader = peReader.GetMetadataReader();
        int blobHeapOffset = BlobHeapOffset(origBytes, peReader.PEHeaders, corHeader);

        List<MutationOp> ops = [];
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AddDependencyPatch(new DependencyPatchRequest
            {
                Patched = patched,
                Reader = reader,
                Handle = handle,
                TargetNames = targetNames,
                BlobHeapStart = blobHeapOffset,
                Ops = ops
            });
        }

        return ops.Count == 0 ? null : new SnDependencyWork(patched, [.. ops]);
    }

    private static void AddDependencyPatch(DependencyPatchRequest request)
    {
        AssemblyReference assemblyRef = request.Reader.GetAssemblyReference(request.Handle);
        string refName = request.Reader.GetString(assemblyRef.Name);
        if (!request.TargetNames.Any(name => string.Equals(name, refName, StringComparison.OrdinalIgnoreCase)))
            return;

        BlobHandle publicKeyTokenHandle = assemblyRef.PublicKeyOrToken;
        if (publicKeyTokenHandle.IsNil) return;

        byte[] publicKeyToken = request.Reader.GetBlobBytes(publicKeyTokenHandle);
        if (publicKeyToken.Length == 0) return;

        int rowIndex = MetadataTokens.GetRowNumber(request.Handle);
        int heapOffset = MetadataTokens.GetHeapOffset(publicKeyTokenHandle);
        int writeOffset = request.BlobHeapStart + heapOffset + PeUtils.BlobPrefixLen(publicKeyToken.Length);
        request.Patched.AsSpan(writeOffset, publicKeyToken.Length).Clear();
        request.Ops.Add(new MutationOp(
            "snstrip.dep",
            new PlanTarget("asmref.token", Table: "AssemblyRef", Row: rowIndex, Offset: writeOffset),
            HexUtils.Hex(publicKeyToken),
            Zeros(publicKeyToken.Length)));
    }

    private static bool HasSignedIvt(MetadataReader reader)
    {
        foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!AttrReader.IsMatch(reader, attr, "System.Runtime.CompilerServices", "InternalsVisibleToAttribute"))
                continue;

            string? name = AttrReader.ReadFixedString(attr, 0);
            if (name is not null && name.Contains(", PublicKey=", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private sealed class SelfStripRequest
    {
        public required byte[] Bytes { get; init; }
        public required PEHeaders Headers { get; init; }
        public required CorHeader CorHeader { get; init; }
        public required MetadataReader Reader { get; init; }
        public required bool HadIvt { get; init; }
        public required string AssemblyName { get; init; }
    }

    private sealed class SelfPatch
    {
        public required byte[] Before { get; init; }
        public required byte[] After { get; init; }
        public required PEHeaders Headers { get; init; }
        public required CorHeader CorHeader { get; init; }
        public required MetadataReader Reader { get; init; }
        public required int BlobOffset { get; init; }
        public required List<MutationOp> Ops { get; init; }
    }

    private sealed record DependencyPatchRequest
    {
        public required byte[] Patched { get; init; }
        public required MetadataReader Reader { get; init; }
        public required AssemblyReferenceHandle Handle { get; init; }
        public required IReadOnlyList<string> TargetNames { get; init; }
        public required int BlobHeapStart { get; init; }
        public required List<MutationOp> Ops { get; init; }
    }

    private sealed class SnOpTarget
    {
        public required string Kind { get; init; }
        public required int Offset { get; init; }
        public required string Before { get; init; }
        public required string After { get; init; }
    }
}
