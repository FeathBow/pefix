using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ClassTests
{
    [Theory]
    [InlineData("F01_compatible_anycpu.dll", Category.Portability, Status.Compatible)]
    [InlineData("F02_x64only_managed.dll", Category.Portability, Status.Fixable)]
    [InlineData("F03_x64_strongname.dll", Category.Portability, Status.Cautioned)]
    [InlineData("F04_x64_pinvoke.dll", Category.Portability, Status.Cautioned)]
    [InlineData("F05_reference_assembly.dll", Category.RefAssembly, Status.Unsafe)]
    [InlineData("F06_mixed_mode.dll", Category.MixedMode, Status.Unsafe)]
    [InlineData("F07_native_pe.dll", Category.NativeBinary, Status.Unsafe)]
    public void Inspect_Map(string fixture, Category category, Status status)
    {
        var result = PeAnalyzer.Inspect(Paths.Get(fixture));
        Assert.Equal(category, result.Category);
        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData("F08_corrupt.dll")]
    [InlineData("F09_empty.dll")]
    public void Inspect_Bad(string fixture)
    {
        var result = PeAnalyzer.Inspect(Paths.Get(fixture));
        Assert.Equal(Status.Corrupt, result.Status);
    }

    [Fact]
    public void F10_Api()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F10_windows_only.dll"));
        Assert.Equal(Status.Unsafe, result.Status);
        Assert.Equal(Category.PlatformApi, result.Category);
        Assert.Contains("windows", result.OsPlatforms ?? []);
    }

    [Fact]
    public void F11_R2R()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F11_r2r.dll"));
        Assert.Equal(Category.R2R, result.Category);
        Assert.Equal(Status.Cautioned, result.Status);
        Assert.Equal(true, result.HasR2R);
    }

    [Fact]
    public void F12_Trim()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F12_trimmable.dll"));
        Assert.Equal(Category.Trimmable, result.Category);
        Assert.Equal(Status.Cautioned, result.Status);
        Assert.Equal(true, result.IsTrimmable);
    }

    [Fact]
    public void F13_Bundle()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F13_bundle.dll"));
        Assert.Equal(Category.Bundle, result.Category);
        Assert.Equal(Status.Cautioned, result.Status);
    }

    [Fact]
    public void F14_Webcil()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F14_webcil.wasm"));
        Assert.Equal(Category.Webcil, result.Category);
        Assert.Equal(Status.Unsafe, result.Status);
    }

    [Fact]
    public void F15_Sat()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F15_satellite.dll"));
        Assert.Equal(Category.Satellite, result.Category);
        Assert.Equal(Status.Unsafe, result.Status);
    }

    [Fact]
    public void F01_NoNest()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.NotEqual(Category.ModuleNest, result.Category);
    }

    [Fact]
    public void F01_NoMulti()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.NotEqual(Category.MultiModule, result.Category);
    }

    [Fact]
    public void F16_TfmBad()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F16_netfx_stub.dll"));
        Assert.Equal(Category.TfmMismatch, result.Category);
        Assert.Equal(Status.Unsafe, result.Status);
        Assert.Equal("net48", result.Tfm);
    }

    [Fact]
    public void F01_NoSat()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.NotEqual(Category.Satellite, result.Category);
    }

    [Fact]
    public void F01_HasAsm()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.NotNull(result.AssemblyDef);
        Assert.Equal("CompatibleAnyCpu", result.AssemblyDef.Value.Name);
    }

    [Fact]
    public void Scan_Dir()
    {
        string fixturesDir = Path.GetDirectoryName(Paths.Get("F01_compatible_anycpu.dll"))!;
        ScanReport report = Scanner.Scan(fixturesDir);
        Assert.NotNull(report.Results);
        Assert.NotNull(report.Conflicts);
    }

    [Fact]
    public void Status_Order()
    {
        Assert.True((int)Status.Compatible < (int)Status.Fixable);
        Assert.True((int)Status.Fixable < (int)Status.Cautioned);
        Assert.True((int)Status.Cautioned < (int)Status.Unsafe);
        Assert.True((int)Status.Unsafe < (int)Status.Corrupt);
    }
}
