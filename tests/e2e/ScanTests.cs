namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_Groups()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Group: portability", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll [fixable]", result.Stdout);
        Assert.Contains("Group: mixed_mode", result.Stdout);
    }

    [Fact]
    public void Scan_Json()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"status\": \"compatible\"", result.Stdout);
        Assert.Contains("\"status\": \"fixable\"", result.Stdout);
        Assert.Contains("\"by_action\"", result.Stdout);
        Assert.Contains("\"none\"", result.Stdout);
        Assert.Contains("\"fix\"", result.Stdout);
    }

    [Fact]
    public void Scan_Fixable()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "fixable");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_Unsafe()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "unsafe");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_BadFail()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "bad");
        Assert.Equal(2, result.ExitCode);
    }


    public void Dispose()
    {
        _temp.Dispose();
    }
}
