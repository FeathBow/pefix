using System.Reflection.Metadata;
using PeFix.Patch;
using PeFix.Plan;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class VerifiedWriteBatchTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TargetFail()
    {
        string path = MakeRef("batch-first.dll", new Version(9, 0, 0, 0));
        string patchedPath = MakeRef("batch-patched.dll", new Version(13, 0, 0, 0));
        string blockedPath = Path.Combine(_temp.DirPath, "blocked-target.dll");
        Directory.CreateDirectory(blockedPath);

        byte[] original = File.ReadAllBytes(path);
        byte[] patched = File.ReadAllBytes(patchedPath);

        Exception ex = Assert.ThrowsAny<Exception>(() =>
            VerifiedWrite.ApplyBatch([
                MakeRequest(path, original, patched, _ => { }),
                MakeRequest(blockedPath, original, patched, _ => { })
            ]));

        Assert.True(ex is IOException or UnauthorizedAccessException, ex.GetType().FullName);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
        Assert.True(Directory.Exists(blockedPath));
        Assert.False(File.Exists(blockedPath + ".pefix-plan.json"));
        Assert.Equal(new Version(9, 0, 0, 0), ReadVersion(path));
    }

    [Fact]
    public void VerifyFail()
    {
        string path = MakeRef("batch-verify.dll", new Version(9, 0, 0, 0));
        byte[] original = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            VerifiedWrite.ApplyBatch([
                MakeRequest(path, original, original, _ => throw new InvalidOperationException("verify failed"))
            ]));

        Assert.Contains("verify failed", ex.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void DupTarget()
    {
        string path = MakeRef("batch-dup.dll", new Version(9, 0, 0, 0));
        byte[] original = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            VerifiedWrite.ApplyBatch([
                MakeRequest(path, original, original, _ => { }),
                MakeRequest(path, original, original, _ => { })
            ]));

        Assert.Contains("Duplicate write target", ex.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void BackupFail()
    {
        string path = MakeRef("batch-sidecar.dll", new Version(9, 0, 0, 0));
        string patchedPath = MakeRef("batch-sidecar-patched.dll", new Version(13, 0, 0, 0));
        string blockedPath = Path.Combine(_temp.DirPath, "blocked-sidecar.dll");
        Directory.CreateDirectory(blockedPath);

        byte[] original = File.ReadAllBytes(path);
        byte[] patched = File.ReadAllBytes(patchedPath);
        string sidecarPath = path + ".pefix-plan.json";
        string sidecarBefore = "{\"sentinel\":true}";
        File.WriteAllText(sidecarPath, sidecarBefore);

        try
        {
            Exception ex = Assert.ThrowsAny<Exception>(() =>
                VerifiedWrite.ApplyBatch([
                    MakeRequest(path, original, patched, _ => { }, backup: true),
                    MakeRequest(blockedPath, original, patched, _ => { }, backup: true)
                ]));

            Assert.True(ex is IOException or UnauthorizedAccessException, ex.GetType().FullName);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(sidecarBefore, File.ReadAllText(sidecarPath));
            Assert.False(File.Exists(path + ".bak"));
        }
        finally
        {
            if (File.Exists(sidecarPath))
                File.Delete(sidecarPath);
        }
    }

    [Fact]
    public void RollbackFail()
    {
        string path = MakeRef("batch-ro.dll", new Version(9, 0, 0, 0));
        string patchedPath = MakeRef("batch-ro-patched.dll", new Version(13, 0, 0, 0));
        string blockedPath = Path.Combine(_temp.DirPath, "blocked-ro.dll");
        Directory.CreateDirectory(blockedPath);

        byte[] original = File.ReadAllBytes(path);
        byte[] patched = File.ReadAllBytes(patchedPath);

        try
        {
            var ex = Assert.Throws<AggregateException>(() =>
                VerifiedWrite.ApplyBatch([
                    MakeRequest(path, original, patched, MakeReadOnly),
                    MakeRequest(blockedPath, original, patched, _ => { })
                ]));

            Assert.Contains("rollback also failed", ex.Message);
            Assert.Equal(2, ex.InnerExceptions.Count);
            Assert.Contains("Rollback failed for target", ex.InnerExceptions[1].Message);
            Assert.Equal(patched, File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".pefix-plan.json"));
        }
        finally
        {
            MakeWritable(path);
        }
    }

    private string MakeRef(string fileName, Version refVersion)
    {
        string path = Path.Combine(_temp.DirPath, fileName);
        RefPe.WriteVersionRef(path, "Newtonsoft.Json", refVersion);
        return path;
    }

    private static Version ReadVersion(string path)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
            {
                AssemblyReference r = reader.GetAssemblyReference(h);
                if (reader.GetString(r.Name) == "Newtonsoft.Json")
                    return r.Version;
            }

            throw new InvalidOperationException($"AssemblyRef 'Newtonsoft.Json' not found in {path}.");
        });
    }

    private static VerifiedWrite.Request MakeRequest(
        string path,
        byte[] original,
        byte[] patched,
        Action<string> verify,
        bool backup = false)
    {
        return new VerifiedWrite.Request
        {
            Path = path,
            Original = original,
            Patched = patched,
            Ops = [new MutationOp("test.write", new PlanTarget("test"), "00", "01")],
            Backup = backup,
            Verify = verify
        };
    }

    private static void MakeReadOnly(string path)
    {
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    private static void MakeWritable(string path)
    {
        if (!File.Exists(path))
            return;

        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
    }
}
