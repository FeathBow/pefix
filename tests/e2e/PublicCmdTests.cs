namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class PublicCmdTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("publicize")]
    [InlineData("publicise")]
    public void PublicizeJsonDryRun(string verb)
    {
        string path = _temp.Copy("F19_internals.dll");
        byte[] before = FileAssert.ReadBytes(path);

        CliResult result = CliRunner.Run(verb, path, "--json");

        Assert.Equal(0, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.True(root.GetProperty("dry_run").GetBoolean());
        Assert.True(root.GetProperty("ops_count").GetInt32() > 0);
        var target = Assert.Single(root.GetProperty("targets").EnumerateArray(), item =>
            item.GetProperty("table").GetString() == "TypeDef");
        Assert.True(target.GetProperty("row").GetInt32() > 0);
        Assert.Equal("guided_fix", root.GetProperty("repair_class").GetString());
        string[] risks = JsonAssert.StringArray(root.GetProperty("unverified_risks"));
        Assert.Contains("API compatibility", risks[0]);
        Assert.Contains("encapsulation safety", risks[0]);
        Assert.Contains("runtime behavior", risks[0]);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void PublicizeDryRunBlock()
    {
        string path = _temp.Copy("F19_internals.dll");

        CliResult result = CliRunner.Run("publicize", path);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Details:", result.Stdout);
        Assert.Contains("Targets:", result.Stdout);
        Assert.Contains("TypeDef row", result.Stdout);
        Assert.Contains("Repair Class:", result.Stdout);
        Assert.Contains("guided_fix", result.Stdout);
        Assert.Contains("Not Proven:", result.Stdout);
        Assert.Contains("API compatibility", result.Stdout);
        Assert.Contains("encapsulation safety", result.Stdout);
        Assert.Contains("runtime behavior", result.Stdout);
    }

    [Fact]
    public void PublicizeRefuseFileJson()
    {
        string path = _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("publicize", path, "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.EndsWith("F07_native_pe.dll", root.GetProperty("path").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
    }
}
