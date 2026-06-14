using PeFix.Meta;

namespace PeFix.Tests;

public sealed class NativeScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void ModuleKey_NormalizesExtensionAndLibPrefix()
    {
        Assert.Equal("sqlite3", NativeScan.ModuleKey("sqlite3"));
        Assert.Equal("sqlite3", NativeScan.ModuleKey("SQLite3.dll"));
        Assert.Equal("sqlite3", NativeScan.ModuleKey("libsqlite3.so"));
        Assert.Equal("sqlite3", NativeScan.ModuleKey("libsqlite3.dylib"));
    }

    [Fact]
    public void IsSystemModule_SuppressesOsAndApiSets()
    {
        Assert.True(NativeScan.IsSystemModule("kernel32"));
        Assert.True(NativeScan.IsSystemModule("USER32.dll"));
        Assert.True(NativeScan.IsSystemModule("libc"));
        Assert.True(NativeScan.IsSystemModule("api-ms-win-core-file-l1-1-0.dll"));
        // Runtime-internal (QCall), Windows OS (httpapi), and IIS hosting
        // (aspnetcorev2_inprocess) targets are environment-provided, not folder deps.
        Assert.True(NativeScan.IsSystemModule("qcall"));
        Assert.True(NativeScan.IsSystemModule("httpapi.dll"));
        Assert.True(NativeScan.IsSystemModule("aspnetcorev2_inprocess.dll"));
        Assert.False(NativeScan.IsSystemModule("gameplaynative"));
    }

    [Fact]
    public void FindNativeGaps_FlagsAbsentModuleOnly()
    {
        File.WriteAllBytes(Path.Combine(_temp.DirPath, "libshipped.so"), [0x1]);
        Inspection consumer = Consumer("/d/app.dll", ["shipped", "absent", "user32"]);

        NativeGap[] gaps = NativeScan.FindNativeGaps([consumer], _temp.DirPath);

        NativeGap gap = Assert.Single(gaps);
        Assert.Equal("absent", gap.ModuleName);
        Assert.Null(gap.PresentPath);
    }

    [Fact]
    public void IsAbsolutePath_DetectsOsLocatedTargets()
    {
        Assert.True(NativeScan.IsAbsolutePath("/System/Library/Frameworks/Network.framework/Network"));
        Assert.True(NativeScan.IsAbsolutePath("/usr/lib/libproc.dylib"));
        Assert.True(NativeScan.IsAbsolutePath(@"C:\Windows\System32\httpapi.dll"));
        Assert.False(NativeScan.IsAbsolutePath("gameplaynative"));
        Assert.False(NativeScan.IsAbsolutePath("libfoo.dylib"));
    }

    [Fact]
    public void FindNativeGaps_IgnoresAbsoluteOsPaths()
    {
        Inspection consumer = Consumer("/d/app.dll", ["/usr/lib/libproc.dylib", "absent"]);

        NativeGap[] gaps = NativeScan.FindNativeGaps([consumer], _temp.DirPath);

        NativeGap gap = Assert.Single(gaps);
        Assert.Equal("absent", gap.ModuleName);
    }

    [Fact]
    public void FindNativeGaps_FlagsMachineMismatchForArchLockedConsumer()
    {
        string nativePath = Path.Combine(_temp.DirPath, "engine.dll");
        File.WriteAllBytes(nativePath, [0x1]);
        Inspection consumer = Consumer("/d/app.dll", ["engine"], peFormat: "PE32+", machine: "AMD64");
        Inspection native = NativeFile(nativePath, "I386");

        NativeGap[] gaps = NativeScan.FindNativeGaps([consumer, native], _temp.DirPath);

        NativeGap gap = Assert.Single(gaps);
        Assert.Equal("I386", gap.PresentMachine);
        Assert.Equal("AMD64", gap.RequiredMachine);
    }

    [Fact]
    public void FindNativeGaps_AnyCpuConsumerSkipsMachineCheck()
    {
        string nativePath = Path.Combine(_temp.DirPath, "engine.dll");
        File.WriteAllBytes(nativePath, [0x1]);
        Inspection consumer = Consumer("/d/app.dll", ["engine"]);
        Inspection native = NativeFile(nativePath, "I386");

        Assert.Empty(NativeScan.FindNativeGaps([consumer, native], _temp.DirPath));
    }

    private static Inspection Consumer(
        string path,
        string[] modules,
        string peFormat = "PE32",
        string machine = "I386")
    {
        return new Inspection(
            path,
            true,
            true,
            peFormat,
            machine,
            default,
            default,
            Category.Portability,
            Status.Compatible,
            "portable",
            "cause",
            [],
            [],
            [],
            null,
            modules,
            null,
            null,
            null,
            null,
            new AssemblyIdentity("App", "1.0.0.0"));
    }

    private static Inspection NativeFile(string path, string machine)
    {
        return new Inspection(
            path,
            true,
            false,
            "PE32",
            machine,
            default,
            default,
            Category.NativeBinary,
            Status.Unsafe,
            "native_binary",
            "cause",
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    public void Dispose() => _temp.Dispose();
}
