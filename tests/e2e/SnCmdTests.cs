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

        var root = JsonAssert.ParseObject(result.Stdout);
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    [Fact]
    public void SnStripVerb_NoApplyFlag_DryRunsOnly()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        byte[] before = FileAssert.ReadBytes(path);
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void SnStripVerb_DryRun_BlockFormat()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
        Assert.Contains("Action:  Run:", result.Stdout);
        Assert.Contains("Details:", result.Stdout);
    }
}
