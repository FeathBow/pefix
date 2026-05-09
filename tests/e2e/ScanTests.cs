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
        var result = CliRunner.Run(_temp.DirPath);
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
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement gate = root.GetProperty("gate");
        JsonElement results = root.GetProperty("results");
        JsonElement summary = root.GetProperty("summary");
        JsonElement issues = root.GetProperty("issues");

        Assert.Equal("pass", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal("pass", gate.GetProperty("conflict").GetString());
        Assert.Equal(0, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(0, gate.GetProperty("issue_codes").GetArrayLength());
        Assert.Equal(0, issues.GetArrayLength());
        Assert.Equal(0, summary.GetProperty("issues").GetInt32());
        Assert.Equal("portable", results[0].GetProperty("reason_code").GetString());
        Assert.Equal("none", results[0].GetProperty("action").GetString());
        Assert.Equal("non_portable", results[1].GetProperty("reason_code").GetString());
        Assert.Equal("fix", results[1].GetProperty("action").GetString());
        Assert.Equal(1, summary.GetProperty("by_action").GetProperty("none").GetInt32());
        Assert.Equal(1, summary.GetProperty("by_action").GetProperty("fix").GetInt32());
    }

    [Fact]
    public void Scan_Miss()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run(_temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Missing refs (2):", result.Stdout);
        Assert.Contains("Dependency: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.Contains("Microsoft.Extensions.DependencyInjection: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.Contains("Install the missing managed dependency into the scanned directory", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_MissJs()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement missing = root.GetProperty("missing_refs");
        JsonElement issues = root.GetProperty("issues");
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(2, missing.GetArrayLength());
        Assert.Equal(2, issues.GetArrayLength());
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal("pass", gate.GetProperty("conflict").GetString());
        Assert.Equal(2, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(2, summary.GetProperty("issues").GetInt32());
        Assert.Equal(2, summary.GetProperty("by_issue").GetProperty("missing_ref").GetInt32());
        Assert.False(issues[0].TryGetProperty("level", out _));
        Assert.Equal("Dependency", missing[0].GetProperty("assembly").GetString());
        Assert.Equal("F18_missing_refs.dll", missing[0].GetProperty("required_by").GetString());
    }

    [Fact]
    public void Scan_DupText()
    {
        CopyDup();
        var result = CliRunner.Run(_temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Dup providers (1):", result.Stdout);
        Assert.Contains("CompatibleAnyCpu: PluginA.dll, PluginB.dll", result.Stdout);
        Assert.Contains("Keep only one provider copy for this assembly name", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_DupJson()
    {
        CopyDup();
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement dups = root.GetProperty("dup_providers");
        JsonElement issues = root.GetProperty("issues");
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(1, dups.GetArrayLength());
        Assert.Equal(1, issues.GetArrayLength());
        Assert.Equal("dup_provider", issues[0].GetProperty("code").GetString());
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal("pass", gate.GetProperty("conflict").GetString());
        Assert.Equal(1, summary.GetProperty("dup_providers").GetInt32());
        Assert.Equal(1, summary.GetProperty("issues").GetInt32());
        Assert.Equal("CompatibleAnyCpu", dups[0].GetProperty("assembly").GetString());
        Assert.Equal(2, dups[0].GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void Scan_DupRel()
    {
        CopyDupNest();
        var text = CliRunner.Run(_temp.DirPath);
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, text.ExitCode);
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement dups = doc.RootElement.GetProperty("dup_providers");
        JsonElement issueFiles = doc.RootElement.GetProperty("issues")[0].GetProperty("files");
        JsonElement summary = doc.RootElement.GetProperty("summary");
        string fileA = Path.Combine("a", "Plugin.dll");
        string fileB = Path.Combine("b", "Plugin.dll");

        Assert.Contains("2 require attention.", text.Stdout);
        Assert.Equal(2, dups[0].GetProperty("files").GetArrayLength());
        Assert.Equal(fileA, dups[0].GetProperty("files")[0].GetString());
        Assert.Equal(fileB, dups[0].GetProperty("files")[1].GetString());
        Assert.Equal(2, issueFiles.GetArrayLength());
        Assert.Equal(fileA, issueFiles[0].GetString());
        Assert.Equal(fileB, issueFiles[1].GetString());
        Assert.Equal(1, summary.GetProperty("issues").GetInt32());
    }

    [Fact]
    public void Scan_DupOk()
    {
        CopyDup();
        var result = CliRunner.Run(_temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_Fixable()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F02_x64only_managed.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on", "fixable");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_Unsafe()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F06_mixed_mode.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on", "unsafe");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_BadFail()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on", "bad");
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void Scan_ConfOk()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_ConfHit()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on-conflict");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_ConfJs()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement gate = doc.RootElement.GetProperty("gate");
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("fail", gate.GetProperty("version_conflict").GetString());
        Assert.Equal("fail", gate.GetProperty("conflict").GetString());
    }

    [Fact]
    public void Scan_ConfRel()
    {
        CopyConfNest();
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement conflict = doc.RootElement.GetProperty("conflicts")[0];
        string referencedBy = Path.Combine("refs", "Consumer.dll");
        string providedBy = Path.Combine("providers", "CompatibleAnyCpu.dll");

        Assert.Equal(referencedBy, conflict.GetProperty("referenced_by").GetString());
        Assert.Equal(providedBy, conflict.GetProperty("provided_by").GetString());
    }

    [Fact]
    public void Scan_MissOk()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run(_temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_MissRel()
    {
        CopyMissNest();
        var result = CliRunner.Run(_temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement missing = doc.RootElement.GetProperty("missing_refs");
        string requiredBy = Path.Combine("refs", "F18_missing_refs.dll");

        Assert.Equal(2, missing.GetArrayLength());
        Assert.Equal(requiredBy, missing[0].GetProperty("required_by").GetString());
        Assert.Equal(requiredBy, missing[1].GetProperty("required_by").GetString());
    }

    private void CopyDup()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginA.dll"), overwrite: true);
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginB.dll"), overwrite: true);
    }

    private void CopyDupNest()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        string dirA = Path.Combine(_temp.DirPath, "a");
        string dirB = Path.Combine(_temp.DirPath, "b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        File.Copy(source, Path.Combine(dirA, "Plugin.dll"), overwrite: true);
        File.Copy(source, Path.Combine(dirB, "Plugin.dll"), overwrite: true);
    }

    private void CopyConfNest()
    {
        string providerDir = Path.Combine(_temp.DirPath, "providers");
        string refDir = Path.Combine(_temp.DirPath, "refs");
        Directory.CreateDirectory(providerDir);
        Directory.CreateDirectory(refDir);
        File.Copy(Paths.Get("F01_compatible_anycpu.dll"), Path.Combine(providerDir, "CompatibleAnyCpu.dll"), overwrite: true);
        File.Copy(Paths.Get("F17_conflict.dll"), Path.Combine(refDir, "Consumer.dll"), overwrite: true);
    }

    private void CopyMissNest()
    {
        string refDir = Path.Combine(_temp.DirPath, "refs");
        Directory.CreateDirectory(refDir);
        File.Copy(Paths.Get("F18_missing_refs.dll"), Path.Combine(refDir, "F18_missing_refs.dll"), overwrite: true);
    }

    public void Dispose() => _temp.Dispose();
}
