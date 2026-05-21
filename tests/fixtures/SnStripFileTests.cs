using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PeFix.Cli;
using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class SnStripFileTests : IDisposable
{
    private static readonly byte[] StrongNameToken = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11];
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void DepFailSelf()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        Directory.CreateDirectory(sibling + ".pefix-plan.json");
        byte[] before = File.ReadAllBytes(target);

        SnStripResult result = SnStripper.Strip(target, new SnStripOpts(Backup: false));

        Assert.False(result.WasPatched);
        Assert.Single(result.DepFails);
        Assert.Equal(before, File.ReadAllBytes(target));
        Assert.True(ReadCorFlags(target).HasFlag(CorFlags.StrongNameSigned));
        Assert.Equal(StrongNameToken, ReadToken(sibling, "X64StrongName"));
    }

    [Fact]
    public void DepRefText()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        Directory.CreateDirectory(sibling + ".pefix-plan.json");

        string text = SnStripOut.Render(SnStripper.Strip(target, new SnStripOpts(Backup: false)));

        Assert.Contains("Dependency rewrite was refused", text);
        Assert.Contains("Strong Name:", text);
        Assert.Contains("Yes", text);
        Assert.DoesNotContain("nothing to strip", text);
    }

    [Fact]
    public void DrySeesDep()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        byte[] beforeTarget = File.ReadAllBytes(target);
        byte[] beforeSibling = File.ReadAllBytes(sibling);

        SnStripResult result = SnStripper.Strip(target, new SnStripOpts(DryRun: true));

        Assert.True(result.WasDryRun);
        Assert.Equal(1, result.DepsPatched);
        Assert.Single(result.Deps);
        Assert.Equal(beforeTarget, File.ReadAllBytes(target));
        Assert.Equal(beforeSibling, File.ReadAllBytes(sibling));
        Assert.False(File.Exists(target + ".pefix-plan.json"));
        Assert.False(File.Exists(sibling + ".pefix-plan.json"));
    }

    [Fact]
    public void DryIgnoresDepPlanPath()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        Directory.CreateDirectory(sibling + ".pefix-plan.json");
        byte[] beforeTarget = File.ReadAllBytes(target);
        byte[] beforeSibling = File.ReadAllBytes(sibling);

        SnStripResult result = SnStripper.Strip(target, new SnStripOpts(DryRun: true));

        Assert.True(result.WasDryRun);
        Assert.False(result.WasPatched);
        Assert.Empty(result.DepFails);
        Assert.Single(result.Deps);
        Assert.Equal(beforeTarget, File.ReadAllBytes(target));
        Assert.Equal(beforeSibling, File.ReadAllBytes(sibling));
    }

    private static CorFlags ReadCorFlags(string path)
    {
        return PeRead.Pe(path, pe => pe.PEHeaders.CorHeader!.Flags);
    }

    private static byte[] ReadToken(string path, string name)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
            {
                AssemblyReference r = reader.GetAssemblyReference(h);
                if (reader.GetString(r.Name) == name)
                    return reader.GetBlobBytes(r.PublicKeyOrToken);
            }

            throw new InvalidOperationException($"AssemblyRef '{name}' was not found in {path}.");
        });
    }
}
