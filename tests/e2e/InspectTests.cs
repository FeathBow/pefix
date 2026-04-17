
namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class InspectTests
{
    [Fact]
    public void Inspect_Ok()
    {
        var result = CliRunner.Run(Paths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:        compatible", result.Stdout);
    }

    [Fact]
    public void FixJson()
    {
        var result = CliRunner.Run(Paths.Get("F02_x64only_managed.dll"), "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        Assert.Contains("\"status\": \"fixable\"", result.Stdout);
    }

    [Fact]
    public void Unsafe()
    {
        var result = CliRunner.Run(Paths.Get("F06_mixed_mode.dll"), "--fail-on", "cautioned");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status:        unsafe", result.Stdout);
    }

    [Fact]
    public void BadFail()
    {
        var result = CliRunner.Run(Paths.Get("F01_compatible_anycpu.dll"), "--fail-on", "typo");
        Assert.Equal(2, result.ExitCode);
    }

    [Theory]
    [InlineData("F01_compatible_anycpu.dll", "none")]
    [InlineData("F02_x64only_managed.dll", "fix")]
    [InlineData("F03_x64_strongname.dll", "fix")]
    [InlineData("F11_r2r.dll", "acknowledge")]
    [InlineData("F06_mixed_mode.dll", "blocked")]
    public void Action(string fixture, string action)
    {
        var result = CliRunner.Run(Paths.Get(fixture), "--json");
        Assert.Contains($"\"action\": \"{action}\"", result.Stdout);
    }

    [Theory]
    [InlineData("F01_compatible_anycpu.dll", "portable")]
    [InlineData("F02_x64only_managed.dll", "non_portable")]
    [InlineData("F05_reference_assembly.dll", "ref_assembly")]
    [InlineData("F06_mixed_mode.dll", "mixed_mode")]
    [InlineData("F16_netfx_stub.dll", "tfm_mismatch")]
    public void Inspect_Code(string fixture, string reasonCode)
    {
        var result = CliRunner.Run(Paths.Get(fixture), "--json");
        Assert.Contains($"\"reason_code\": \"{reasonCode}\"", result.Stdout);
    }
}
