using System;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class PublishGateTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_FailOnIssue_ExitsNonZeroWhenDirectoryIssuesPresent()
    {
        // A publish/plugin folder missing a managed dependency: the #1 publish-dir
        // CI failure. The gate must fail the build, not just report.
        _temp.CopyAll("F18_missing_refs.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_FailOnIssue_ExitsZeroWhenClean()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_FailOnIssue_ExitsNonZeroForReferenceAssembly()
    {
        _temp.CopyAll("F05_reference_assembly.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_FailOnIssue_ExitsNonZeroForCorruptFile()
    {
        _temp.CopyAll("F08_corrupt.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_JsonGateFailsForBlockingFileDiagnostics()
    {
        _temp.CopyAll("F05_reference_assembly.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement gate = JsonAssert.ParseObject(result.Stdout).GetProperty("gate");
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal(0, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(1, gate.GetProperty("blocking_file_count").GetInt32());
        Assert.Equal(["ref_assembly"], JsonAssert.StringArray(gate.GetProperty("blocking_file_reasons")));
    }

    [Fact]
    public void Scan_PublishDirDoesNotRunBepInExPluginFolderIssues()
    {
        _temp.CopyAll("F27_bep_miss.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            "bep_missing",
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    public void Dispose() => _temp.Dispose();
}
