
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class InspectTests
{
    [Fact]
    public void Inspect_Ok()
    {
        var result = CliRunner.Run("inspect", Paths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:         compatible", result.Stdout);
    }

    [Fact]
    public void Inspect_Json()
    {
        var result = RunJson("F02_x64only_managed.dll");
        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        Assert.Equal("fixable", JsonAssert.ParseObject(result.Stdout).GetProperty("status").GetString());
    }

    [Fact]
    public void Inspect_Json_ShowsBepInExPluginMeta()
    {
        JsonElement plugin = JsonAssert.ParseObject(RunJson("F26_bep_meta.dll").Stdout)
            .GetProperty("bepinex")
            .GetProperty("plugins")[0];

        Assert.Equal("test.meta", plugin.GetProperty("guid").GetString());
        Assert.Equal("Meta Plugin", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    [Fact]
    public void Unsafe()
    {
        var result = CliRunner.Run("inspect", Paths.Get("F06_mixed_mode.dll"), "--fail-on", "cautioned");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status:         unsafe", result.Stdout);
    }

    [Fact]
    public void BadFail()
    {
        var result = CliRunner.Run("inspect", Paths.Get("F01_compatible_anycpu.dll"), "--fail-on", "typo");
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
        JsonElement root = JsonAssert.ParseObject(RunJson(fixture).Stdout);
        Assert.Equal(action, root.GetProperty("action").GetString());
    }

    [Theory]
    [InlineData("F01_compatible_anycpu.dll", "portable")]
    [InlineData("F02_x64only_managed.dll", "non_portable")]
    [InlineData("F05_reference_assembly.dll", "ref_assembly")]
    [InlineData("F06_mixed_mode.dll", "mixed_mode")]
    [InlineData("F16_netfx_stub.dll", "tfm_mismatch")]
    public void Inspect_Code(string fixture, string reasonCode)
    {
        JsonElement root = JsonAssert.ParseObject(RunJson(fixture).Stdout);
        Assert.Equal(reasonCode, root.GetProperty("reason_code").GetString());
    }

    private static CliResult RunJson(string fixture)
    {
        return CliRunner.Run("inspect", Paths.Get(fixture), "--json");
    }
}
