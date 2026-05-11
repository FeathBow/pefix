namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class FixTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Fix_Fixable()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var result = CliRunner.Run("fix", path, "--apply");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  PATCHED", result.Stdout);
        Assert.Contains("re-inspection passed", result.Stdout);
    }

    [Fact]
    public void Fix_Ok()
    {
        var path = _temp.Copy("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("fix", path, "--apply");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  UNCHANGED", result.Stdout);
        Assert.Contains("skipped (already compatible)", result.Stdout);
    }

    [Fact]
    public void Fix_Unsafe()
    {
        var path = _temp.Copy("F06_mixed_mode.dll");
        var result = CliRunner.Run("fix", path, "--apply");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("cannot be patched safely", result.Stderr);
    }

    [Fact]
    public void Fix_Dir()
    {
        _temp.Copy("F01_compatible_anycpu.dll");
        var fixablePath = _temp.Copy("F02_x64only_managed.dll");
        _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run("fix", _temp.DirPath, "--apply");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Processed 3 candidate files. Patched 1, unchanged 1, dry-run 0, refused 1.", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll", result.Stdout);
        Assert.Contains("F06_mixed_mode.dll", result.Stdout);
        Assert.True(File.Exists(fixablePath + ".bak"));
    }

    [Fact]
    public void Fix_DirBad()
    {
        _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run("fix", _temp.DirPath, "--apply");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Processed 1 candidate files. Patched 0, unchanged 0, dry-run 0, refused 1.", result.Stdout);
    }

    [Fact]
    public void Fix_JsonDir()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll", "F06_mixed_mode.dll");

        var result = CliRunner.Run("fix", _temp.DirPath, "--apply", "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("directory").GetString()));
        Assert.Equal(3, root.GetProperty("summary").GetProperty("total_candidates").GetInt32());
        Assert.Contains(root.GetProperty("results").EnumerateArray(), item => item.GetProperty("was_patched").GetBoolean());
        Assert.Single(root.GetProperty("refusals").EnumerateArray());
    }

    [Fact]
    public void Fix_JsonBad()
    {
        var path = _temp.Copy("F06_mixed_mode.dll");

        var result = CliRunner.Run("fix", path, "--apply", "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
        var before = root.GetProperty("before");
        Assert.Equal("mixed_mode", before.GetProperty("reason_code").GetString());
        Assert.Equal("unsafe", before.GetProperty("status").GetString());
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
