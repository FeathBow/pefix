namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class FixTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Fix_Fixable()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var result = CliRunner.Run(path, "--fix");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Verify:  Re-inspection passed. Assembly manifest was validated.", result.Stdout);
    }

    [Fact]
    public void Fix_Compatible()
    {
        var path = _temp.Copy("F01_compatible_anycpu.dll");
        var result = CliRunner.Run(path, "--fix");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Result:  No changes were needed", result.Stdout);
        Assert.Contains("Verify:  Skipped because the assembly was already compatible.", result.Stdout);
    }

    [Fact]
    public void Fix_Unsafe()
    {
        var path = _temp.Copy("F06_mixed_mode.dll");
        var result = CliRunner.Run(path, "--fix");
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("cannot be patched safely", result.Stderr);
    }

    [Fact]
    public void Fix_Directory()
    {
        _temp.Copy("F01_compatible_anycpu.dll");
        var fixablePath = _temp.Copy("F02_x64only_managed.dll");
        _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run(_temp.DirPath, "--fix");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Processed 3 candidate files. Patched 1, unchanged 1, dry-run 0, refused 1.", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll", result.Stdout);
        Assert.Contains("F06_mixed_mode.dll", result.Stdout);
        Assert.True(File.Exists(fixablePath + ".bak"));
    }

    [Fact]
    public void Fix_DirUnsafe()
    {
        _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run(_temp.DirPath, "--fix");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Processed 1 candidate files. Patched 0, unchanged 0, dry-run 0, refused 1.", result.Stdout);
    }

    [Fact]
    public void Fix_JsonBatch()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll", "F06_mixed_mode.dll");

        var result = CliRunner.Run(_temp.DirPath, "--fix", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        Assert.Contains("\"directory\":", result.Stdout);
        Assert.Contains("\"summary\":", result.Stdout);
        Assert.Contains("\"was_patched\": true", result.Stdout);
        Assert.Contains("\"refusals\":", result.Stdout);
    }

    [Fact]
    public void Fix_JsonUnsafe()
    {
        var path = _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run(path, "--fix", "--json");

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        Assert.Contains("\"reason\":", result.Stdout);
        Assert.Contains("\"reason_code\": \"mixed_mode\"", result.Stdout);
        Assert.Contains("\"status\": \"unsafe\"", result.Stdout);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
