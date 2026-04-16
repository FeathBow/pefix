using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_Groups()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Group: portability", result.Stdout);
        Assert.Contains("F02_x64only_managed.dll [fixable]", result.Stdout);
        Assert.Contains("Group: mixed_mode", result.Stdout);
    }

    [Fact]
    public void Scan_Json()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"status\": \"compatible\"", result.Stdout);
        Assert.Contains("\"status\": \"fixable\"", result.Stdout);
        Assert.Contains("\"reason_code\": \"portable\"", result.Stdout);
        Assert.Contains("\"reason_code\": \"non_portable\"", result.Stdout);
        Assert.Contains("\"by_action\"", result.Stdout);
        Assert.Contains("\"none\"", result.Stdout);
        Assert.Contains("\"fix\"", result.Stdout);
    }

    [Fact]
    public void Scan_Miss()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Missing refs (2):", result.Stdout);
        Assert.Contains("Dependency: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.Contains("Microsoft.Extensions.DependencyInjection: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_MissJs()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\n  \"missing_refs\": [", result.Stdout);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        Assert.Contains("\"assembly\": \"Dependency\"", result.Stdout);
        Assert.Contains("\"assembly\": \"Microsoft.Extensions.DependencyInjection\"", result.Stdout);
        Assert.Contains("\"required_by\": \"F18_missing_refs.dll\"", result.Stdout);
    }

    [Fact]
    public void Scan_DupText()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Dup providers (1):", result.Stdout);
        Assert.Contains("CompatibleAnyCpu: PluginA.dll, PluginB.dll", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_DupJson()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement dups = root.GetProperty("dup_providers");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(1, dups.GetArrayLength());
        Assert.Equal(1, summary.GetProperty("dup_providers").GetInt32());
        Assert.Equal("CompatibleAnyCpu", dups[0].GetProperty("assembly").GetString());
        Assert.Equal(2, dups[0].GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void Scan_DupOk()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_Fixable()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "fixable");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_Unsafe()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "unsafe");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_BadFail()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "bad");
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void Scan_ConfOk()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_ConfHit()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(1, result.ExitCode);
    }

    private void CopyDup()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginA.dll"), overwrite: true);
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginB.dll"), overwrite: true);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
