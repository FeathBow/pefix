using System.IO;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class RootTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Root_Start()
    {
        var result = CliRunner.Run();
        Assert.Contains("Commands:", result.Stdout);
        Assert.Contains("inspect", result.Stdout);
        Assert.Contains("scan", result.Stdout);
        Assert.Contains("fix", result.Stdout);
    }

    [Fact]
    public void Inspect_File()
    {
        var result = CliRunner.Run("inspect", Paths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:        compatible", result.Stdout);
    }

    [Fact]
    public void Scan_Dir()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"pefix {Path.GetFileName(_temp.DirPath)}", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll [fixable]", result.Stdout);
    }

    [Fact]
    public void FixVerb_DryRun()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var result = CliRunner.Run("fix", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
    }

    [Fact]
    public void Root_Help()
    {
        var result = CliRunner.Run("--help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pefix is a single-binary CLI", result.Stdout);
        Assert.Contains("Exit codes:", result.Stdout);
        Assert.Contains("snstrip", result.Stdout);
    }

    [Fact]
    public void FixVerb_NoApplyFlag_DryRunsOnly()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var before = File.GetLastWriteTimeUtc(path);
        var result = CliRunner.Run("fix", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void FixVerb_WithApplyFlag_WritesFile()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var before = File.GetLastWriteTimeUtc(path);
        var result = CliRunner.Run("fix", path, "--apply");
        Assert.Equal(0, result.ExitCode);
        Assert.NotEqual(before, File.GetLastWriteTimeUtc(path));
        Assert.True(File.Exists(path + ".bak"));
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
