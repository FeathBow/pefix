using System.Reflection.Metadata;
using PeFix.Patch;
using PeFix.Plan;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class RedirTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    private string MakeRef(string fileName, string refName, Version refVersion)
    {
        string path = Path.Combine(_temp.DirPath, fileName);
        RefPe.WriteVerRef(path, refName, refVersion);
        return path;
    }

    [Fact]
    public void Rewrites()
    {
        string path = MakeRef("a.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RedirPatch.Redir(path, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        Assert.Equal(new Version(13, 0, 0, 0), ReadRefVer(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void NameMiss()
    {
        string path = MakeRef("b.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RedirResult r = RedirPatch.Redir(path, new RedirOptions("OtherLib", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        Assert.Equal(0, r.RowsPatched);
        Assert.Equal(new Version(9, 0, 0, 0), ReadRefVer(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void VersionMiss()
    {
        string path = MakeRef("c.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RedirResult r = RedirPatch.Redir(path, new RedirOptions("Newtonsoft.Json", new Version(8, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        Assert.Equal(0, r.RowsPatched);
        Assert.Equal(new Version(9, 0, 0, 0), ReadRefVer(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void DrySame()
    {
        string path = MakeRef("d.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);
        RedirResult r = RedirPatch.Redir(path, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), DryRun: true));
        Assert.True(r.WasDryRun);
        Assert.Equal(1, r.RowsPatched);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void PlanMade()
    {
        string path = MakeRef("e.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RedirResult result = RedirPatch.Redir(path, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        Assert.Equal(path + ".pefix-plan.json", result.PlanPath);
        Assert.True(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void PlanRead()
    {
        string path = MakeRef("f.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RedirPatch.Redir(path, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        PefixPlan plan = PlanJson.Read(File.ReadAllText(path + ".pefix-plan.json"));
        Assert.Equal(1, plan.Version);
        Assert.Equal("pefix", plan.Tool.Name);
        Assert.Single(plan.Ops);
        Assert.Equal("redir.version", plan.Ops[0].Kind);
        Assert.Equal("AssemblyRef", plan.Ops[0].Target.Table);
    }

    [Fact]
    public void DirHits()
    {
        MakeRef("g1.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        MakeRef("g2.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        MakeRef("g3.dll", "OtherLib", new Version(9, 0, 0, 0));

        RedBatch batch = RedirPatch.RedirDir(_temp.DirPath, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), Backup: false));
        Assert.Equal(2, batch.Results.Length);
        Assert.Empty(batch.Refusals);
        Assert.All(batch.Results, r => Assert.Equal(1, r.RowsPatched));
    }

    [Fact]
    public void DirRefs()
    {
        MakeRef("h1.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        _temp.Copy("F07_native_pe.dll");
        RedBatch batch = RedirPatch.RedirDir(_temp.DirPath, new RedirOptions("Newtonsoft.Json", new Version(9, 0, 0, 0), new Version(13, 0, 0, 0), DryRun: true));
        Assert.Single(batch.Results);
        Assert.Single(batch.Refusals);
        Assert.EndsWith("F07_native_pe.dll", batch.Refusals[0].Path);
    }

    private static Version ReadRefVer(string path, string refName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
            {
                AssemblyReference ar = reader.GetAssemblyReference(h);
                if (string.Equals(reader.GetString(ar.Name), refName, StringComparison.Ordinal))
                    return ar.Version;
            }
            throw new InvalidOperationException($"AssemblyRef '{refName}' not found in {path}.");
        });
    }

}
