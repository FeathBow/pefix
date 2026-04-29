namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class PinCmdTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void PinRefuse()
    {
        _temp.Copy("F04_x64_pinvoke.dll");
        _temp.Copy("F07_native_pe.dll");
        CliResult result = CliRunner.Run("pinvoke", _temp.DirPath, "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"refusals\"", result.Stdout);
        Assert.Contains("F07_native_pe.dll", result.Stdout);
    }
}
