using System.Reflection.Metadata;
using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class RedirGuardTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ApplyRefusesUnencodableVersionBeforeWrite()
    {
        string path = MakeRef("verify.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(ushort.MaxValue + 1, 0, 0, 0),
                    Backup: false)));

        Assert.Contains("Target version must have four numeric fields", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void ApplyRefusesTwoPartVersionBeforeWrite()
    {
        string path = MakeRef("verify-two.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(13, 0),
                    Backup: false)));

        Assert.Contains("Target version must have four numeric fields", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ApplyRefusesThreePartVersionBeforeWrite()
    {
        string path = MakeRef("verify-three.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(13, 0, 0),
                    Backup: false)));

        Assert.Contains("Target version must have four numeric fields", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void DryRunRefusesUnencodableVersionBeforeWrite()
    {
        string path = MakeRef("dry-verify.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(ushort.MaxValue + 1, 0, 0, 0),
                    DryRun: true)));

        Assert.Contains("Target version must have four numeric fields", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void DryRunIgnoresPlanPath()
    {
        string path = MakeRef("dry-plan-blocked.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);
        Directory.CreateDirectory(path + ".pefix-plan.json");

        RedirResult result = RedirPatch.Redir(
            path,
            new RedirOptions(
                "Newtonsoft.Json",
                new Version(9, 0, 0, 0),
                new Version(13, 0, 0, 0),
                DryRun: true));

        Assert.True(result.WasDryRun);
        Assert.Single(result.Ops);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(new Version(9, 0, 0, 0), ReadAssemblyRefVersion(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void PlanWriteFailureKeepsFileUnchanged()
    {
        string path = MakeRef("plan-blocked.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);
        Directory.CreateDirectory(path + ".pefix-plan.json");

        Assert.ThrowsAny<IOException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(13, 0, 0, 0),
                    Backup: false)));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(new Version(9, 0, 0, 0), ReadAssemblyRefVersion(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void VerifyFailRollsBack()
    {
        string path = MakeRef("verify-fails.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedirPatch.Redir(
                path,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(13, 0, 0, 0),
                    Backup: true),
                (_, _, _) => throw new InvalidOperationException("verify failed")));

        Assert.Contains("verify failed", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
        Assert.False(File.Exists(path + ".bak"));
        Assert.Equal(new Version(9, 0, 0, 0), ReadAssemblyRefVersion(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void DirDryRunIgnoresPlanPath()
    {
        MakeRef("dry-dir-hit.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        string blocked = MakeRef("dry-dir-blocked.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        Directory.CreateDirectory(blocked + ".pefix-plan.json");

        RedBatch batch = RedirPatch.RedirDir(
            _temp.DirPath,
            new RedirOptions(
                "Newtonsoft.Json",
                new Version(9, 0, 0, 0),
                new Version(13, 0, 0, 0),
                DryRun: true));

        Assert.Equal(2, batch.Results.Length);
        Assert.Empty(batch.Refusals);
        Assert.All(batch.Results, result => Assert.True(result.WasDryRun));
    }

    [Fact]
    public void DirPreflightFailureKeepsCandidatesUnchanged()
    {
        string candidate = MakeRef("blocked-a.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        string blocked = MakeRef("blocked-b.dll", "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = File.ReadAllBytes(candidate);
        Directory.CreateDirectory(blocked + ".pefix-plan.json");

        Assert.ThrowsAny<IOException>(() =>
            RedirPatch.RedirDir(
                _temp.DirPath,
                new RedirOptions(
                    "Newtonsoft.Json",
                    new Version(9, 0, 0, 0),
                    new Version(13, 0, 0, 0),
                    Backup: false)));

        Assert.Equal(before, File.ReadAllBytes(candidate));
        Assert.Equal(new Version(9, 0, 0, 0), ReadAssemblyRefVersion(candidate, "Newtonsoft.Json"));
    }

    private string MakeRef(string fileName, string refName, Version refVersion)
    {
        string path = Path.Combine(_temp.DirPath, fileName);
        RefPe.WriteVersionRef(path, refName, refVersion);
        return path;
    }

    private static Version ReadAssemblyRefVersion(string path, string refName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = reader.GetAssemblyReference(handle);
                if (string.Equals(reader.GetString(assemblyRef.Name), refName, StringComparison.Ordinal))
                    return assemblyRef.Version;
            }
            throw new InvalidOperationException($"AssemblyRef '{refName}' not found in {path}.");
        });
    }
}
