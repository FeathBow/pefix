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
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pefix <path>", result.Stdout);
        Assert.Contains("pefix ./mods --fix --dry-run", result.Stdout);
    }

    [Fact]
    public void Root_File()
    {
        var result = CliRunner.Run(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:        compatible", result.Stdout);
    }

    [Fact]
    public void Root_Dir()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run(_temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"pefix {Path.GetFileName(_temp.DirPath)}", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll [fixable]", result.Stdout);
    }

    [Fact]
    public void Root_Fix()
    {
        var path = _temp.Copy("F02_x64only_managed.dll");
        var result = CliRunner.Run(path, "--fix", "--dry-run");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
    }

    [Fact]
    public void Root_BadConf()
    {
        var result = CliRunner.Run(Paths.Get("F01_compatible_anycpu.dll"), "--fail-on-conflict");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("directory scan", result.Stderr);
    }

    [Fact]
    public void Root_BadFix()
    {
        var result = CliRunner.Run(Paths.Get("F02_x64only_managed.dll"), "--dry-run");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("only with --fix", result.Stderr);
    }

    [Fact]
    public void Root_Help()
    {
        var result = CliRunner.Run("--help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("<path>", result.Stdout);
        Assert.Contains("--fix", result.Stdout);
        Assert.Contains("Single-file inspect exits with code 1", result.Stdout);
        Assert.Contains("snstrip", result.Stdout);
    }

    [Fact]
    public void Inspect_VerbAndPathFirst_ProduceSameOutput()
    {
        var path = Paths.Get("F02_x64only_managed.dll");
        var verbResult = CliRunner.Run("inspect", path);
        var pathFirstResult = CliRunner.Run(path);
        Assert.Equal(verbResult.ExitCode, pathFirstResult.ExitCode);
        Assert.Equal(verbResult.Stdout, pathFirstResult.Stdout);
    }

    [Fact]
    public void Scan_VerbAndPathFirst_ProduceSameOutput()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var verbResult = CliRunner.Run("scan", _temp.DirPath);
        var pathFirstResult = CliRunner.Run(_temp.DirPath);
        Assert.Equal(verbResult.ExitCode, pathFirstResult.ExitCode);
        Assert.Equal(verbResult.Stdout, pathFirstResult.Stdout);
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
