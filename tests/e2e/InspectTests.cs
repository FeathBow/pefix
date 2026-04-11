
namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class InspectTests
{
    [Fact]
    public void InspectText_ForCompatibleAssembly_ReturnsSuccess()
    {
        var result = CliRunner.Run("inspect", FixturePaths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  COMPATIBLE", result.Stdout);
        Assert.Contains("Status:        compatible", result.Stdout);
    }

    [Fact]
    public void InspectJson_ForFixableAssembly_ReturnsIssueExitCode()
    {
        var result = CliRunner.Run("inspect", FixturePaths.Get("F02_x64only_managed.dll"), "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"status\": \"fixable\"", result.Stdout);
    }

    [Fact]
    public void InspectOption_FailOnFixable_ForUnsafeAssembly_ReturnsSuccessExitCode()
    {
        var result = CliRunner.Run("inspect", FixturePaths.Get("F06_mixed_mode.dll"), "--fail-on-fixable");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:        unsafe", result.Stdout);
    }
}
