namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ScanTests : IDisposable
{
    private readonly TempFixture _temp = new();

    [Fact]
    public void Scan_Groups()
    {
        _temp.CopyFixtures("F01_compatible_anycpu.dll", "F02_x64only_managed.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Group: portability", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll [fixable]", result.Stdout);
        Assert.Contains("Group: mixed_mode", result.Stdout);
    }

    [Fact]
    public void Scan_Json()
    {
        _temp.CopyFixtures("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"status\": \"compatible\"", result.Stdout);
        Assert.Contains("\"status\": \"fixable\"", result.Stdout);
    }

    [Fact]
    public void Scan_Fixable()
    {
        _temp.CopyFixtures("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-fixable");
        Assert.Equal(1, result.ExitCode);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
