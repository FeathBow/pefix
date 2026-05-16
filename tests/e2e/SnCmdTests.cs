using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class SnCmdTests : IDisposable
{
    private static readonly byte[] StrongNameToken = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11];
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SnRefuse()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");
        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--json");
        Assert.Equal(1, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    [Fact]
    public void SnStripVerb_NoApplyFlag_DryRunsOnly()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        byte[] before = FileAssert.ReadBytes(path);
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void SnStripVerb_DryRun_BlockFormat()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
        Assert.Contains("Action:  Run:", result.Stdout);
        Assert.Contains("Details:", result.Stdout);
    }

    [Fact]
    public void SnStripVerb_ApplyJson_WritesAndKeepsContract()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        CliResult result = CliRunner.Run("snstrip", path, "--apply", "--no-backup", "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.True(root.GetProperty("was_patched").GetBoolean());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        Assert.False(root.TryGetProperty("verify", out _));
        Assert.False(ReadCorFlags(path).HasFlag(CorFlags.StrongNameSigned));
        Assert.All(ReadAssemblyPublicKey(path), b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void SnStripVerb_DirApplyJson_RewritesSiblingToken()
    {
        _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--no-backup", "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Single(root.GetProperty("deps").EnumerateArray());
        byte[] token = ReadAssemblyRefPublicKeyToken(sibling, "X64StrongName");
        Assert.NotEmpty(token);
        Assert.All(token, b => Assert.Equal((byte)0, b));
    }

    private static CorFlags ReadCorFlags(string path)
    {
        return PeRead.Pe(path, pe => pe.PEHeaders.CorHeader!.Flags);
    }

    private static byte[] ReadAssemblyPublicKey(string path)
    {
        return PeRead.Meta(path, reader =>
            reader.GetBlobBytes(reader.GetAssemblyDefinition().PublicKey));
    }

    private static byte[] ReadAssemblyRefPublicKeyToken(string path, string name)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = reader.GetAssemblyReference(h);
                if (reader.GetString(assemblyRef.Name) == name)
                    return reader.GetBlobBytes(assemblyRef.PublicKeyOrToken);
            }
            throw new InvalidOperationException($"AssemblyRef '{name}' was not found in {path}.");
        });
    }
}
