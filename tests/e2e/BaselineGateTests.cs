using System;
using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BaselineGateTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly TempDir _store = new();

    [Fact]
    public void Scan_BaselineFileMissing_ExitsUsage()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--baseline", BaselinePath());

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void Scan_WriteBaselineWithoutBaseline_ExitsUsage()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--write-baseline");

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void Scan_WriteBaselineThenRescan_SuppressesKnownIssues()
    {
        // The CI adoption loop: accept today's known issues once, then gate
        // every later scan on new issues only.
        _temp.CopyAll("F18_missing_refs.dll");
        string baseline = BaselinePath();

        CliResult write = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--baseline", baseline, "--write-baseline");
        Assert.Equal(0, write.ExitCode);
        Assert.NotEmpty(File.ReadAllLines(baseline));

        CliResult rescan = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--baseline", baseline, "--json");
        Assert.Equal(0, rescan.ExitCode);
        JsonElement json = JsonAssert.ParseObject(rescan.Stdout).GetProperty("baseline");
        Assert.Equal(baseline, json.GetProperty("path").GetString());
        Assert.True(json.GetProperty("matched").GetInt32() > 0);
        Assert.Empty(JsonAssert.StringArray(json.GetProperty("new")));
        Assert.Empty(JsonAssert.StringArray(json.GetProperty("stale")));
    }

    [Fact]
    public void Scan_BaselineNewIssue_ExitsNonZero()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        string baseline = BaselinePath();
        File.WriteAllLines(baseline, Array.Empty<string>());

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--baseline", baseline, "--json");

        Assert.Equal(1, result.ExitCode);
        JsonElement json = JsonAssert.ParseObject(result.Stdout).GetProperty("baseline");
        Assert.NotEmpty(JsonAssert.StringArray(json.GetProperty("new")));
    }

    [Fact]
    public void Scan_BaselineStaleEntry_PassesAndReports()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        string baseline = BaselinePath();
        File.WriteAllLines(baseline, ["missing_ref|Gone.Lib|Gone.dll"]);

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--baseline", baseline, "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement json = JsonAssert.ParseObject(result.Stdout).GetProperty("baseline");
        Assert.Equal(["missing_ref|Gone.Lib|Gone.dll"], JsonAssert.StringArray(json.GetProperty("stale")));
    }

    [Fact]
    public void Scan_BaselineTextOutput_ShowsSection()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        string baseline = BaselinePath();
        File.WriteAllLines(baseline, Array.Empty<string>());

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--baseline", baseline);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Baseline:", result.Stdout);
        Assert.Contains("- new: missing_ref|", result.Stdout);
    }

    [Fact]
    public void Scan_WithoutBaseline_KeepsJsonShape()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(JsonAssert.ParseObject(result.Stdout).TryGetProperty("baseline", out _));
    }

    public void Dispose()
    {
        _temp.Dispose();
        _store.Dispose();
    }

    private string BaselinePath()
    {
        return Path.Combine(_store.DirPath, "pefix-baseline.txt");
    }
}
