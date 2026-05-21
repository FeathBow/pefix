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

        var root = JsonAssert.ParseObject(result.Stdout);
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    [Fact]
    public void PinRefuseFileJson()
    {
        string path = _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("pinvoke", path, "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.EndsWith("F07_native_pe.dll", root.GetProperty("path").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
    }
}
