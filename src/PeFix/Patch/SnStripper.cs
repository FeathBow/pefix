using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Meta;
using PeFix.Plan;

namespace PeFix.Patch;

public static class SnStripper
{
    private const int CorFlagOfs = 16;

    public static SnStripRes Strip(string path, SnStripOpts options)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);
        SelfResult self = StripSelf(origBytes, options);

        if (options.DryRun)
            return new SnStripRes(fullPath, null, null, false, true, self.HadIvt, 0, [], []);

        if (!self.WasSigned)
            return new SnStripRes(fullPath, null, null, false, false, self.HadIvt, 0, [], []);

        string? backupPath = options.Backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteAtomic(fullPath, self.Patched);
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, self.Patched, PeUtils.ReadMvid(self.Patched)),
            self.Ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);
        DepScan depScan = ScanSiblings(Path.GetDirectoryName(fullPath)!, [self.AsmName], fullPath, options.Backup);
        return new SnStripRes(fullPath, backupPath, planPath, true, false, self.HadIvt, depScan.Deps.Length, depScan.Deps, depScan.Refusals);
    }

    public static SnBatch StripDir(string dir, SnStripOpts options)
    {
        string fullDir = Path.GetFullPath(dir);
        List<SnStripRes> results = [];
        List<Refusal> refusals = [];
        List<string> stripNames = [];

        foreach (string dll in Directory.EnumerateFiles(fullDir, "*.dll"))
        {
            try
            {
                byte[] origBytes = File.ReadAllBytes(dll);
                SelfResult self = StripSelf(origBytes, options);
                if (!self.WasSigned) continue;

                if (options.DryRun)
                {
                    results.Add(new SnStripRes(dll, null, null, false, true, self.HadIvt, 0, [], []));
                    continue;
                }

                string? backupPath = options.Backup ? PeUtils.Backup(dll) : null;
                PeUtils.WriteAtomic(dll, self.Patched);
                PlanEmit.Write(dll,
                    PlanFileInfo.Describe(dll, origBytes, PeUtils.ReadMvid(origBytes)),
                    PlanFileInfo.Describe(dll, self.Patched, PeUtils.ReadMvid(self.Patched)),
                    self.Ops, backupPath);
                stripNames.Add(self.AsmName);
                results.Add(new SnStripRes(
                    dll,
                    backupPath,
                    PlanEmit.SidecarPath(dll),
                    true,
                    false,
                    self.HadIvt,
                    0,
                    [],
                    []));
            }
            catch (UnsafeException ex) { refusals.Add(new Refusal(dll, ex.Message, PeAnalyzer.Inspect(dll))); }
            catch (InvalidOperationException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }

        SnDep[] deps = [];
        if (!options.DryRun && stripNames.Count > 0)
        {
            DepScan depScan = ScanSiblings(fullDir, stripNames, skipPath: null, options.Backup);
            deps = depScan.Deps;
            refusals.AddRange(depScan.Refusals);
        }

        return new SnBatch(fullDir, [.. results], [.. refusals], deps);
    }

    private readonly record struct SelfResult(byte[] Patched, bool HadIvt, string AsmName, bool WasSigned, IReadOnlyList<MutationOp> Ops);
    private readonly record struct DepScan(SnDep[] Deps, Refusal[] Refusals);

    private static SelfResult StripSelf(byte[] bytes, SnStripOpts options)
    {
        using var readStream = new MemoryStream(bytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("No CLI header — not a managed assembly.");
        MetadataReader reader = peReader.GetMetadataReader();

        bool hadIvt = HasSignedIvt(reader);
        if (hadIvt && !options.Force)
            throw new UnsafeException(
                "This assembly uses InternalsVisibleTo with a signed PublicKey. " +
                "Use --force to strip anyway.");

        string asmName = reader.GetString(reader.GetAssemblyDefinition().Name);

        bool isSigned = corHeader.Flags.HasFlag(CorFlags.StrongNameSigned)
            || !reader.GetAssemblyDefinition().PublicKey.IsNil;

        if (options.DryRun || !isSigned)
            return new SelfResult(bytes, hadIvt, asmName, isSigned, []);

        byte[] patched = (byte[])bytes.Clone();
        List<MutationOp> ops = new();

        int metaRva = corHeader.MetadataDirectory.RelativeVirtualAddress;
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, metaRva);
        int blobOfs = PeUtils.FindHeap(bytes, metaOffset, "#Blob");

        int flagOfs = peReader.PEHeaders.CorHeaderStartOffset + CorFlagOfs;
        uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(flagOfs, 4));
        uint flagsAfter = flagsBefore & ~(uint)CorFlags.StrongNameSigned;
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(flagOfs, 4), flagsAfter);
        ops.Add(MakeOp("corflags", flagOfs,
            HexUtils.Hex(BitConverter.GetBytes(flagsBefore)),
            HexUtils.Hex(BitConverter.GetBytes(flagsAfter))));

        DirectoryEntry snDir = corHeader.StrongNameSignatureDirectory;
        if (snDir.Size > 0)
        {
            int snOffset = PeUtils.RvaToOffset(peReader.PEHeaders, snDir.RelativeVirtualAddress);
            byte[] snBefore = bytes.AsSpan(snOffset, snDir.Size).ToArray();
            patched.AsSpan(snOffset, snDir.Size).Clear();
            ops.Add(MakeOp("snsig", snOffset, HexUtils.Hex(snBefore), Zeros(snDir.Size)));
        }

        BlobHandle pkHandle = reader.GetAssemblyDefinition().PublicKey;
        if (!pkHandle.IsNil)
        {
            byte[] pkContent = reader.GetBlobBytes(pkHandle);
            if (pkContent.Length > 0)
            {
                int heapOffset = MetadataTokens.GetHeapOffset(pkHandle);
                int writeOfs = blobOfs + heapOffset + PeUtils.BlobPrefixLen(pkContent.Length);
                byte[] pkBefore = bytes.AsSpan(writeOfs, pkContent.Length).ToArray();
                patched.AsSpan(writeOfs, pkContent.Length).Clear();
                ops.Add(MakeOp("asm.publickey", writeOfs, HexUtils.Hex(pkBefore), Zeros(pkContent.Length)));
            }
        }

        return new SelfResult(patched, hadIvt, asmName, isSigned, ops);
    }

    private static MutationOp MakeOp(string targetKind, int offset, string before, string after) =>
        new("snstrip", new PlanTarget(targetKind, Offset: offset), before, after);

    private static string Zeros(int len) => new('0', len * 2);

    private static DepScan ScanSiblings(string dir, List<string> targetNames, string? skipPath, bool backup)
    {
        List<SnDep> deps = [];
        List<Refusal> refusals = [];
        foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
        {
            if (skipPath != null && string.Equals(dll, skipPath, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                SnDep? dep = PatchDepDll(dll, targetNames, backup);
                if (dep is { } hit) deps.Add(hit);
            }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (InvalidOperationException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }
        return new DepScan([.. deps], [.. refusals]);
    }

    private static SnDep? PatchDepDll(string path, List<string> targetNames, bool backup)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);
        byte[] patched = (byte[])origBytes.Clone();
        using var stream = new MemoryStream(origBytes, writable: false);
        using var peReader = new PEReader(stream);
        CorHeader? corHeader = peReader.PEHeaders.CorHeader;
        if (corHeader is null) return null;

        MetadataReader reader = peReader.GetMetadataReader();
        int metaOffset = PeUtils.RvaToOffset(peReader.PEHeaders, corHeader.MetadataDirectory.RelativeVirtualAddress);
        int blobOfs = PeUtils.FindHeap(origBytes, metaOffset, "#Blob");

        List<MutationOp> ops = [];
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference asmRef = reader.GetAssemblyReference(handle);
            string refName = reader.GetString(asmRef.Name);
            if (!targetNames.Any(n => string.Equals(n, refName, StringComparison.OrdinalIgnoreCase)))
                continue;
            BlobHandle pkt = asmRef.PublicKeyOrToken;
            byte[] token = reader.GetBlobBytes(pkt);
            if (pkt.IsNil || token.Length == 0) continue;

            int rowIdx = MetadataTokens.GetRowNumber(handle);
            int heapOffset = MetadataTokens.GetHeapOffset(pkt);
            int writeOfs = blobOfs + heapOffset + PeUtils.BlobPrefixLen(token.Length);
            patched.AsSpan(writeOfs, token.Length).Clear();
            ops.Add(new MutationOp(
                "snstrip.dep",
                new PlanTarget("asmref.token", Table: "AssemblyRef", Row: rowIdx, Offset: writeOfs),
                HexUtils.Hex(token),
                Zeros(token.Length)));
        }

        if (ops.Count == 0) return null;
        string? backupPath = backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteAtomic(fullPath, patched);
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, patched, PeUtils.ReadMvid(patched)),
            ops, backupPath);
        return new SnDep(fullPath, backupPath, PlanEmit.SidecarPath(fullPath));
    }

    private static bool HasSignedIvt(MetadataReader reader)
    {
        foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!IsIvtConstructor(reader, attr)) continue;
            CustomAttributeValue<object?> val = attr.DecodeValue(AttrTypes.Instance);
            if (val.FixedArguments.Length > 0
                && val.FixedArguments[0].Value is string name
                && name.Contains(", PublicKey=", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsIvtConstructor(MetadataReader reader, CustomAttribute attr)
    {
        EntityHandle parent = attr.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType(),
            _ => default
        };
        if (parent.IsNil) return false;
        return parent.Kind switch
        {
            HandleKind.TypeReference => IsIvt(reader, reader.GetTypeReference((TypeReferenceHandle)parent)),
            HandleKind.TypeDefinition => IsIvt(reader, reader.GetTypeDefinition((TypeDefinitionHandle)parent)),
            _ => false
        };
    }

    private static bool IsIvt(MetadataReader r, TypeReference t) =>
        string.Equals(r.GetString(t.Namespace), "System.Runtime.CompilerServices", StringComparison.Ordinal) &&
        string.Equals(r.GetString(t.Name), "InternalsVisibleToAttribute", StringComparison.Ordinal);

    private static bool IsIvt(MetadataReader r, TypeDefinition t) =>
        string.Equals(r.GetString(t.Namespace), "System.Runtime.CompilerServices", StringComparison.Ordinal) &&
        string.Equals(r.GetString(t.Name), "InternalsVisibleToAttribute", StringComparison.Ordinal);
}
