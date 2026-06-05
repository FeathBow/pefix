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
        Assert.Contains("F02_x64only_managed.dll [fixable] reason=non_portable action=fix", result.Stdout);
        Assert.Contains("why: This assembly uses a platform-specific header", result.Stdout);
        Assert.Contains("Group: mixed_mode", result.Stdout);
        Assert.Contains("F06_mixed_mode.dll [unsafe] reason=mixed_mode action=blocked", result.Stdout);
    }

    [Fact]
    public void Scan_Json()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement gate = root.GetProperty("gate");
        JsonElement results = root.GetProperty("results");
        JsonElement summary = root.GetProperty("summary");
        JsonElement issues = root.GetProperty("issues");

        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("pass", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal(0, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(0, gate.GetProperty("issue_codes").GetArrayLength());
        Assert.Equal(0, gate.GetProperty("blocking_file_count").GetInt32());
        Assert.Equal(0, gate.GetProperty("blocking_file_reasons").GetArrayLength());
        Assert.Equal(0, issues.GetArrayLength());
        Assert.Equal(0, summary.GetProperty("issues").GetInt32());
        JsonElement ok = JsonAssert.SingleBy(results, "reason_code", "portable");
        JsonElement fix = JsonAssert.SingleBy(results, "reason_code", "non_portable");
        Assert.All(results.EnumerateArray(), item =>
        {
            Assert.Equal(1, item.GetProperty("schema_version").GetInt32());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("bepinex").ValueKind);
        });
        Assert.Equal("none", ok.GetProperty("action").GetString());
        Assert.Equal("fix", fix.GetProperty("action").GetString());
        Assert.Equal(1, summary.GetProperty("by_action").GetProperty("none").GetInt32());
        Assert.Equal(1, summary.GetProperty("by_action").GetProperty("fix").GetInt32());
    }

    [Fact]
    public void Scan_ProfileOutput()
    {
        _temp.CopyAll("F26_bep_meta.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex", "--json");
        Assert.Equal(0, result.ExitCode);

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement profileJson = root.GetProperty("profiles");
        Assert.Equal("unity-bepinex", profileJson.GetProperty("host").GetString());
        Assert.Equal("plugin-folder", profileJson.GetProperty("artifact").GetString());
    }

    [Fact]
    public void Scan_ProfileOutputIncludesDeclaredLoaderTarget()
    {
        _temp.CopyAll("F26_bep_meta.dll");
        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex6-il2cpp", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement profileJson = JsonAssert.ParseObject(result.Stdout).GetProperty("profiles");
        Assert.Equal("unity-bepinex", profileJson.GetProperty("host").GetString());
        Assert.Equal("plugin-folder", profileJson.GetProperty("artifact").GetString());
        Assert.Equal("bepinex6", profileJson.GetProperty("declared_loader_generation").GetString());
        Assert.Equal("il2cpp", profileJson.GetProperty("declared_loader_flavor").GetString());
    }

    [Fact]
    public void Scan_ProfileRejectsUnknown()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--profile", "not-a-real-profile");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unsupported scan profile", result.Stderr);
    }

    [Fact]
    public void Scan_Fixable()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "fixable");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_FixableJsonStillWritesReport()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json", "--fail-on", "fixable");
        Assert.Equal(1, result.ExitCode);

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal("fixable", JsonAssert.SingleBy(root.GetProperty("results"), "reason_code", "non_portable").GetProperty("status").GetString());
    }

    [Fact]
    public void Scan_FixableClean()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "fixable");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_Unsafe()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "unsafe");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_UnsafeIgnoresFixable()
    {
        _temp.CopyAll("F02_x64only_managed.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "unsafe");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_BadFail()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on", "bad");
        Assert.Equal(2, result.ExitCode);
    }

    public void Dispose() => _temp.Dispose();
}
