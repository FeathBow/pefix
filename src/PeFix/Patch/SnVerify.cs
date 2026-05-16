using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Patch;

internal static class SnVerify
{
    public static void SelfStripped(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var peReader = new PEReader(stream);
        CorHeader corHeader = peReader.PEHeaders.CorHeader
            ?? throw Fail(path, "No CLI header after write.");

        if (corHeader.Flags.HasFlag(CorFlags.StrongNameSigned))
            throw Fail(path, "StrongNameSigned flag is still set.");

        MetadataReader reader = peReader.GetMetadataReader();
        BlobHandle publicKeyHandle = reader.GetAssemblyDefinition().PublicKey;
        if (!publicKeyHandle.IsNil && reader.GetBlobBytes(publicKeyHandle).AsSpan().IndexOfAnyExcept((byte)0) >= 0)
            throw Fail(path, "Assembly public key blob is not zeroed.");

        VerifySignature(path, bytes, peReader.PEHeaders, corHeader);
    }

    public static void DepTokensCleared(string path, List<string> targetNames)
    {
        bool sawTargetToken = false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();

        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference assemblyRef = reader.GetAssemblyReference(handle);
            string refName = reader.GetString(assemblyRef.Name);
            if (!targetNames.Any(name => string.Equals(name, refName, StringComparison.OrdinalIgnoreCase)))
                continue;

            BlobHandle tokenHandle = assemblyRef.PublicKeyOrToken;
            if (tokenHandle.IsNil) continue;

            byte[] token = reader.GetBlobBytes(tokenHandle);
            if (token.Length == 0) continue;

            sawTargetToken = true;
            if (token.AsSpan().IndexOfAnyExcept((byte)0) >= 0)
                throw Fail(path, $"AssemblyRef '{refName}' public key token is not zeroed.");
        }

        if (!sawTargetToken)
            throw Fail(path, "No rewritten AssemblyRef public key token was found.");
    }

    private static void VerifySignature(string path, byte[] bytes, PEHeaders headers, CorHeader corHeader)
    {
        DirectoryEntry strongNameDirectory = corHeader.StrongNameSignatureDirectory;
        if (strongNameDirectory.Size == 0) return;

        int strongNameOffset = PeUtils.RvaToOffset(headers, strongNameDirectory.RelativeVirtualAddress);
        if (bytes.AsSpan(strongNameOffset, strongNameDirectory.Size).IndexOfAnyExcept((byte)0) >= 0)
            throw Fail(path, "Strong-name signature blob is not zeroed.");
    }

    private static InvalidOperationException Fail(string path, string reason) =>
        new($"snstrip verification failed for {path}: {reason}");
}
