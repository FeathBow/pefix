using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PeFix.Meta;
using PeFix.Plan;

namespace PeFix.Patch;

public static class SnStripper
{
    private const int CorFlagsOffset = 16;

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
        PeUtils.WriteVerifiedAtomic(fullPath, self.Patched, tmpPath =>
        {
            SnVerify.SelfStripped(tmpPath);
            Validator.Validate(tmpPath);
        });
        PlanEmit.Write(fullPath,
            PlanFileInfo.Describe(fullPath, origBytes, PeUtils.ReadMvid(origBytes)),
            PlanFileInfo.Describe(fullPath, self.Patched, PeUtils.ReadMvid(self.Patched)),
            self.Ops, backupPath);
        string planPath = PlanEmit.SidecarPath(fullPath);
        DepScan depScan = ScanSiblings(Path.GetDirectoryName(fullPath)!, [self.AssemblyName], fullPath, options.Backup);
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
                PeUtils.WriteVerifiedAtomic(dll, self.Patched, tmpPath =>
                {
                    SnVerify.SelfStripped(tmpPath);
                    Validator.Validate(tmpPath);
                });
                PlanEmit.Write(dll,
                    PlanFileInfo.Describe(dll, origBytes, PeUtils.ReadMvid(origBytes)),
                    PlanFileInfo.Describe(dll, self.Patched, PeUtils.ReadMvid(self.Patched)),
                    self.Ops, backupPath);
                stripNames.Add(self.AssemblyName);
                results.Add(new SnStripRes(dll, backupPath, PlanEmit.SidecarPath(dll), true, false, self.HadIvt, 0, [], []));
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

    private readonly record struct SelfResult(byte[] Patched, bool HadIvt, string AssemblyName, bool WasSigned, IReadOnlyList<MutationOp> Ops);
    private readonly record struct SelfPatch(byte[] Before, byte[] After, PEHeaders Headers, CorHeader CorHeader, MetadataReader Reader, int BlobOffset, List<MutationOp> Ops);
    private readonly record struct DepScan(SnDep[] Deps, Refusal[] Refusals);
    private static SelfResult StripSelf(byte[] bytes, SnStripOpts options)
    {
        using var readStream = new MemoryStream(bytes, writable: false);
        using var peReader = new PEReader(readStream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("No CLI header. Not a managed assembly.");
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
        int blobHeapOffset = PeUtils.FindHeap(bytes, metaOffset, "#Blob");

        var patch = new SelfPatch(bytes, patched, peReader.PEHeaders, corHeader, reader, blobHeapOffset, ops);
        ClearStrongNameFlag(patch);
        ClearSignature(patch);
        ClearPublicKey(patch);
        return new SelfResult(patched, hadIvt, asmName, isSigned, ops);
    }

    private static void ClearStrongNameFlag(SelfPatch patch)
    {
        int flagsOffset = patch.Headers.CorHeaderStartOffset + CorFlagsOffset;
        uint flagsBefore = BinaryPrimitives.ReadUInt32LittleEndian(patch.Before.AsSpan(flagsOffset, 4));
        uint flagsAfter = flagsBefore & ~(uint)CorFlags.StrongNameSigned;
        BinaryPrimitives.WriteUInt32LittleEndian(patch.After.AsSpan(flagsOffset, 4), flagsAfter);
        patch.Ops.Add(MakeOp("corflags", flagsOffset,
            HexUtils.Hex(BitConverter.GetBytes(flagsBefore)),
            HexUtils.Hex(BitConverter.GetBytes(flagsAfter))));
    }

    private static void ClearSignature(SelfPatch patch)
    {
        DirectoryEntry strongNameDirectory = patch.CorHeader.StrongNameSignatureDirectory;
        if (strongNameDirectory.Size == 0) return;

        int strongNameOffset = PeUtils.RvaToOffset(patch.Headers, strongNameDirectory.RelativeVirtualAddress);
        byte[] strongNameBefore = patch.Before.AsSpan(strongNameOffset, strongNameDirectory.Size).ToArray();
        patch.After.AsSpan(strongNameOffset, strongNameDirectory.Size).Clear();
        patch.Ops.Add(MakeOp("snsig", strongNameOffset, HexUtils.Hex(strongNameBefore), Zeros(strongNameDirectory.Size)));
    }

    private static void ClearPublicKey(SelfPatch patch)
    {
        BlobHandle publicKeyHandle = patch.Reader.GetAssemblyDefinition().PublicKey;
        if (publicKeyHandle.IsNil) return;
        byte[] publicKey = patch.Reader.GetBlobBytes(publicKeyHandle);
        if (publicKey.Length > 0)
        {
            int heapOffset = MetadataTokens.GetHeapOffset(publicKeyHandle);
            int writeOffset = patch.BlobOffset + heapOffset + PeUtils.BlobPrefixLen(publicKey.Length);
            byte[] publicKeyBefore = patch.Before.AsSpan(writeOffset, publicKey.Length).ToArray();
            patch.After.AsSpan(writeOffset, publicKey.Length).Clear();
            patch.Ops.Add(MakeOp("asm.publickey", writeOffset, HexUtils.Hex(publicKeyBefore), Zeros(publicKey.Length)));
        }
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
        int blobHeapOffset = PeUtils.FindHeap(origBytes, metaOffset, "#Blob");

        List<MutationOp> ops = [];
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference assemblyRef = reader.GetAssemblyReference(handle);
            string refName = reader.GetString(assemblyRef.Name);
            if (!targetNames.Any(name => string.Equals(name, refName, StringComparison.OrdinalIgnoreCase)))
                continue;
            BlobHandle publicKeyTokenHandle = assemblyRef.PublicKeyOrToken;
            if (publicKeyTokenHandle.IsNil) continue;

            byte[] publicKeyToken = reader.GetBlobBytes(publicKeyTokenHandle);
            if (publicKeyToken.Length == 0) continue;

            int rowIndex = MetadataTokens.GetRowNumber(handle);
            int heapOffset = MetadataTokens.GetHeapOffset(publicKeyTokenHandle);
            int writeOffset = blobHeapOffset + heapOffset + PeUtils.BlobPrefixLen(publicKeyToken.Length);
            patched.AsSpan(writeOffset, publicKeyToken.Length).Clear();
            ops.Add(new MutationOp(
                "snstrip.dep",
                new PlanTarget("asmref.token", Table: "AssemblyRef", Row: rowIndex, Offset: writeOffset),
                HexUtils.Hex(publicKeyToken),
                Zeros(publicKeyToken.Length)));
        }

        if (ops.Count == 0) return null;
        string? backupPath = backup ? PeUtils.Backup(fullPath) : null;
        PeUtils.WriteVerifiedAtomic(fullPath, patched, tmpPath =>
        {
            SnVerify.DepTokensCleared(tmpPath, targetNames);
            Validator.Validate(tmpPath);
        });
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
            if (!AttrReader.IsMatch(reader, attr, "System.Runtime.CompilerServices", "InternalsVisibleToAttribute"))
                continue;

            string? name = AttrReader.ReadFixedString(attr, 0);
            if (name is not null && name.Contains(", PublicKey=", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
