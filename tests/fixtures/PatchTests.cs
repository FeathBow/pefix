using System.Runtime.Loader;
using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class PatchTests : IDisposable
{
    private readonly TempFixture _temp = new();

    [Fact]
    public void Fix_Backup()
    {
        var path = _temp.CopyFixture("F02_x64only_managed.dll");
        Patcher.Fix(path, backup: true);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Fix_Patched()
    {
        var path = _temp.CopyFixture("F02_x64only_managed.dll");
        Patcher.Fix(path, backup: true);
        var result = PeAnalyzer.Inspect(path);
        Assert.Equal(Status.Compatible, result.Status);
    }

    [Fact]
    public void Fix_Idem()
    {
        var path = _temp.CopyFixture("F02_x64only_managed.dll");
        Patcher.Fix(path, backup: false);
        var bytes1 = File.ReadAllBytes(path);
        Patcher.Fix(path, backup: false);
        var bytes2 = File.ReadAllBytes(path);
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Fix_DryRun()
    {
        var path = _temp.CopyFixture("F02_x64only_managed.dll");
        var before = File.ReadAllBytes(path);
        Patcher.Fix(path, backup: false, dryRun: true);
        var after = File.ReadAllBytes(path);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Fix_Noop()
    {
        var path = _temp.CopyFixture("F01_compatible_anycpu.dll");
        var result = Patcher.Fix(path, backup: false);
        Assert.False(result.WasPatched);
        Assert.Equal(Status.Compatible, result.After.Status);
    }

    [Fact]
    public void Fix_Loadable()
    {
        var path = _temp.CopyFixture("F02_x64only_managed.dll");
        Patcher.Fix(path, backup: false);
        var weak = LoadCheck(path);
        for (int i = 0; weak.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference LoadCheck(string path)
    {
        var context = new AssemblyLoadContext("pefix-test-load", isCollectible: true);
        var assembly = context.LoadFromAssemblyPath(path);
        Assert.Equal("X64OnlyManaged", assembly.GetName().Name);
        context.Unload();
        return new WeakReference(context);
    }

    [Fact]
    public void Fix_Throws()
    {
        var path = _temp.CopyFixture("F06_mixed_mode.dll");
        var ex = Assert.Throws<UnsafeException>(() => Patcher.Fix(path));
        Assert.Contains("mixed_mode", ex.Message);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
