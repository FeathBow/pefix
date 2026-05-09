namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class SnCmdTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SnRefuse()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");
        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"refusals\"", result.Stdout);
        Assert.Contains("F07_native_pe.dll", result.Stdout);
    }

    [Fact]
    public void SnStripVerb_NoApplyFlag_DryRunsOnly()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        var before = File.GetLastWriteTimeUtc(path);
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }
}
