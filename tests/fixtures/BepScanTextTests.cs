using System.IO;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BepScanTextTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_ShowsBepInExTriageBlock()
    {
        Copy("F27_bep_miss.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Blocking Issues (1):", result.Stdout);
        Assert.Contains("[bep_missing] need.hard", result.Stdout);
        Assert.Contains("repair: assisted_fix", result.Stdout);
        Assert.Contains("verify: pefix scan <path> --json", result.Stdout);
        Assert.True(
            result.Stdout.IndexOf("Blocking Issues", StringComparison.Ordinal) <
            result.Stdout.IndexOf("Group:", StringComparison.Ordinal));
        Assert.Contains("BepInEx deps (1):", result.Stdout);
        Assert.Contains("test.miss requires BepInEx plugin need.hard", result.Stdout);
        Assert.Contains("Install the missing BepInEx plugin dependency", result.Stdout);
    }

    [Fact]
    public void Scan_TextShowsVersionMismatchIssue()
    {
        Copy("F27_bep_miss.dll");
        Copy("F28_bep_need.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[bep_version_mismatch] need.hard", result.Stdout);
        Assert.Contains("requires BepInEx plugin need.hard >=2.0.0", result.Stdout);
        Assert.Contains("repair: assisted_fix", result.Stdout);
        Assert.Contains("verify: pefix scan <path> --json", result.Stdout);
    }

    [Fact]
    public void Scan_TextShowsDuplicateGuidIssue()
    {
        Copy("F26_bep_meta.dll");
        File.Copy(Paths.Get("F26_bep_meta.dll"), Path.Combine(_temp.DirPath, "F26_bep_meta_copy.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[bep_dup_guid] test.meta", result.Stdout);
        Assert.Contains("Multiple BepInEx plugins declare GUID test.meta", result.Stdout);
        Assert.Contains("repair: assisted_fix", result.Stdout);
        Assert.Contains("verify: pefix scan <path> --json", result.Stdout);
    }

    private void Copy(string name)
    {
        File.Copy(Paths.Get(name), Path.Combine(_temp.DirPath, name), overwrite: true);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
