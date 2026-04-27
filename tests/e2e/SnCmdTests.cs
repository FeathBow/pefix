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
        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--dry-run", "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"refusals\"", result.Stdout);
        Assert.Contains("F07_native_pe.dll", result.Stdout);
    }
}
