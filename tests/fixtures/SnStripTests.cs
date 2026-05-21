using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PeFix.Patch;
using PeFix.Plan;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class SnStripTests : IDisposable
{
    private static readonly byte[] StrongNameToken = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11];
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    private string CopyF03() => _temp.Copy("F03_x64_strongname.dll");

    [Fact]
    public void FlagClr()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: false));
        Assert.False(ReadCorFlags(path).HasFlag(CorFlags.StrongNameSigned));
    }

    [Fact]
    public void KeyZero()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: false));
        byte[] pk = ReadKey(path);
        Assert.NotEmpty(pk);
        Assert.All(pk, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void BakOn()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: true));
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void NoBak()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: false));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void DrySame()
    {
        string path = CopyF03();
        byte[] before = File.ReadAllBytes(path);
        SnStripper.Strip(path, new SnStripOpts(DryRun: true));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void DryFlag()
    {
        string path = CopyF03();
        SnStripResult r = SnStripper.Strip(path, new SnStripOpts(DryRun: true));
        Assert.True(r.WasDryRun);
        Assert.Contains(r.Ops, op => op.Target.Kind == "corflags");
    }

    [Fact]
    public void FullPath()
    {
        string path = CopyF03();
        SnStripResult r = SnStripper.Strip(path, new SnStripOpts(Backup: false));
        Assert.Equal(Path.GetFullPath(path), r.Path);
    }

    [Fact]
    public void BakPath()
    {
        string path = CopyF03();
        SnStripResult r = SnStripper.Strip(path, new SnStripOpts(Backup: true));
        Assert.Equal(path + ".bak", r.BackupPath);
    }

    [Fact]
    public void NoBakPath()
    {
        string path = CopyF03();
        SnStripResult r = SnStripper.Strip(path, new SnStripOpts(Backup: false));
        Assert.Null(r.BackupPath);
    }

    [Fact]
    public void NoCli()
    {
        string path = _temp.Copy("F07_native_pe.dll");
        Assert.Throws<RefusalException>(() =>
            SnStripper.Strip(path, new SnStripOpts(Backup: false)));
    }

    [Fact]
    public void TokenZeroed()
    {
        string target = CopyF03();
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        SnStripper.Strip(target, new SnStripOpts(Backup: false));

        byte[] token = ReadToken(sibling, "X64StrongName");
        Assert.NotEmpty(token);
        Assert.All(token, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void DirStrip()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F01_compatible_anycpu.dll");

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(Backup: false));

        Assert.Single(batch.Results);
        Assert.Empty(batch.Refusals);
        Assert.False(ReadCorFlags(batch.Results[0].Path).HasFlag(CorFlags.StrongNameSigned));
    }

    [Fact]
    public void DirSame()
    {
        string f03 = _temp.Copy("F03_x64_strongname.dll");
        byte[] before = File.ReadAllBytes(f03);

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(DryRun: true));

        Assert.Single(batch.Results);
        Assert.Equal(before, File.ReadAllBytes(f03));
    }

    [Fact]
    public void PlanMade()
    {
        string path = CopyF03();
        SnStripResult result = SnStripper.Strip(path, new SnStripOpts(Backup: false));
        Assert.Equal(path + ".pefix-plan.json", result.PlanPath);
        Assert.True(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void NoPlan()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(DryRun: true));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void PlanRead()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: false));
        PefixPlan plan = PlanJson.Read(File.ReadAllText(path + ".pefix-plan.json"));
        Assert.Equal(1, plan.Version);
        Assert.Equal("pefix", plan.Tool.Name);
        Assert.NotEmpty(plan.Ops);
        Assert.Single(plan.Inputs);
        Assert.Single(plan.Outputs);
    }

    [Fact]
    public void PlanFlag()
    {
        string path = CopyF03();
        SnStripper.Strip(path, new SnStripOpts(Backup: false));
        PefixPlan plan = PlanJson.Read(File.ReadAllText(path + ".pefix-plan.json"));
        Assert.Contains(plan.Ops, o => o.Target.Kind == "corflags");
    }

    [Fact]
    public void DirToken()
    {
        _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(Backup: false));

        Assert.Single(batch.Deps);
        byte[] token = ReadToken(sibling, "X64StrongName");
        Assert.NotEmpty(token);
        Assert.All(token, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void DirDrySeesDep()
    {
        _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        byte[] beforeSibling = File.ReadAllBytes(sibling);

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(DryRun: true));

        Assert.Single(batch.Results);
        Assert.Single(batch.Deps);
        Assert.Equal(beforeSibling, File.ReadAllBytes(sibling));
        Assert.False(File.Exists(sibling + ".pefix-plan.json"));
    }

    [Fact]
    public void DirSkipsUnsignedSiblingRef()
    {
        _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteVersionRef(sibling, "X64StrongName", new Version(1, 0, 0, 0));
        byte[] before = File.ReadAllBytes(sibling);

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(Backup: false));

        Assert.Single(batch.Results);
        Assert.Empty(batch.Refusals);
        Assert.Empty(batch.Deps);
        Assert.Equal(before, File.ReadAllBytes(sibling));
    }

    [Fact]
    public void DirRefs()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");

        SnBatch batch = SnStripper.StripDir(_temp.DirPath, new SnStripOpts(DryRun: true));

        Assert.Single(batch.Results);
        Assert.Single(batch.Refusals);
        Assert.EndsWith("F07_native_pe.dll", batch.Refusals[0].Path);
    }

    [Fact]
    public void DirPreflightFailureKeepsCandidatesUnchanged()
    {
        string candidate = _temp.Copy("F03_x64_strongname.dll");
        string blocked = Path.Combine(_temp.DirPath, "blocked.dll");
        File.Copy(candidate, blocked);
        byte[] before = File.ReadAllBytes(candidate);
        Directory.CreateDirectory(blocked + ".pefix-plan.json");

        Assert.ThrowsAny<IOException>(() =>
            SnStripper.StripDir(_temp.DirPath, new SnStripOpts(Backup: false)));

        Assert.Equal(before, File.ReadAllBytes(candidate));
        Assert.True(ReadCorFlags(candidate).HasFlag(CorFlags.StrongNameSigned));
    }

    [Fact]
    public void DirDepPreflightFailureKeepsSelfUnchanged()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        Directory.CreateDirectory(sibling + ".pefix-plan.json");
        byte[] before = File.ReadAllBytes(target);

        Assert.ThrowsAny<IOException>(() =>
            SnStripper.StripDir(_temp.DirPath, new SnStripOpts(Backup: false)));

        Assert.Equal(before, File.ReadAllBytes(target));
        Assert.True(ReadCorFlags(target).HasFlag(CorFlags.StrongNameSigned));
        Assert.Equal(StrongNameToken, ReadToken(sibling, "X64StrongName"));
    }

    private static CorFlags ReadCorFlags(string path)
    {
        return PeRead.Pe(path, pe => pe.PEHeaders.CorHeader!.Flags);
    }

    private static byte[] ReadKey(string path)
    {
        return PeRead.Meta(path, reader =>
            reader.GetBlobBytes(reader.GetAssemblyDefinition().PublicKey));
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
