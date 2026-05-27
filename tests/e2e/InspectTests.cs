using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class InspectTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void InspectOk()
    {
        var result = CliRunner.Run("inspect", Paths.Get("F01_compatible_anycpu.dll"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:         compatible", result.Stdout);
    }

    [Fact]
    public void InspectJson()
    {
        var result = RunJson("F02_x64only_managed.dll");
        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("fixable", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("bepinex").ValueKind);
    }

    [Fact]
    public void InspectJsonBepInExPluginMeta()
    {
        JsonElement plugin = JsonAssert.ParseObject(RunJson("F26_bep_meta.dll").Stdout)
            .GetProperty("bepinex")
            .GetProperty("plugins")[0];

        Assert.Equal("test.meta", plugin.GetProperty("guid").GetString());
        Assert.Equal("Meta Plugin", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    [Fact]
    public void InspectJsonBepInExDependencyMeta()
    {
        JsonElement dep = JsonAssert.ParseObject(RunJson("F27_bep_miss.dll").Stdout)
            .GetProperty("bepinex")
            .GetProperty("plugins")[0]
            .GetProperty("deps")[0];

        Assert.Equal("need.hard", dep.GetProperty("guid").GetString());
        Assert.Equal(">=2.0.0", dep.GetProperty("range").GetString());
        Assert.True(dep.GetProperty("hard").GetBoolean());
        Assert.False(dep.GetProperty("present").GetBoolean());
        Assert.False(dep.GetProperty("case_mismatch").GetBoolean());
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
    [InlineData("F03_x64_strongname.dll", "acknowledge")]
    [InlineData("F11_r2r.dll", "acknowledge")]
    [InlineData("F06_mixed_mode.dll", "blocked")]
    public void Action(string fixture, string action)
    {
        JsonElement root = JsonAssert.ParseObject(RunJson(fixture).Stdout);
        Assert.Equal(action, root.GetProperty("action").GetString());
    }

    [Theory]
    [InlineData("F02_x64only_managed.dll", "auto_fix", "pefix fix")]
    [InlineData("F03_x64_strongname.dll", "guided_fix", "--force")]
    [InlineData("F11_r2r.dll", "diagnostic_only", "runtime version")]
    public void RepairContract(string fixture, string repairClass, string hintPart)
    {
        JsonElement root = JsonAssert.ParseObject(RunJson(fixture).Stdout);
        Assert.Equal(repairClass, root.GetProperty("repair_class").GetString());
        Assert.Contains(hintPart, root.GetProperty("repair_hint").GetString());
    }

    [Theory]
    [InlineData("F01_compatible_anycpu.dll", "portable")]
    [InlineData("F02_x64only_managed.dll", "non_portable")]
    [InlineData("F07_native_pe.dll", "native_binary")]
    [InlineData("F08_corrupt.dll", "corrupt_pe")]
    [InlineData("F10_windows_only.dll", "platform_api")]
    [InlineData("F05_reference_assembly.dll", "ref_assembly")]
    [InlineData("F11_r2r.dll", "r2r")]
    [InlineData("F12_trimmable.dll", "trimmable")]
    [InlineData("F13_bundle.dll", "bundle")]
    [InlineData("F14_webcil.wasm", "webcil")]
    [InlineData("F15_satellite.dll", "satellite")]
    [InlineData("F06_mixed_mode.dll", "mixed_mode")]
    [InlineData("F16_netfx_stub.dll", "tfm_mismatch")]
    public void InspectCode(string fixture, string reasonCode)
    {
        JsonElement root = JsonAssert.ParseObject(RunJson(fixture).Stdout);
        Assert.Equal(reasonCode, root.GetProperty("reason_code").GetString());
    }

    [Theory]
    [InlineData("module-nest.dll", "module_nest")]
    [InlineData("multi-module.dll", "multi_module")]
    public void InspectCodeGeneratedMetadata(string fileName, string reasonCode)
    {
        string path = Path.Combine(_temp.DirPath, fileName);
        WriteFixture(path, fileName);

        JsonElement root = JsonAssert.ParseObject(CliRunner.Run("inspect", path, "--json").Stdout);
        Assert.Equal(reasonCode, root.GetProperty("reason_code").GetString());
    }

    private static void WriteFixture(string path, string fileName)
    {
        switch (fileName)
        {
            case "module-nest.dll":
                RefPe.WriteNested(path);
                break;
            case "multi-module.dll":
                RefPe.WriteMultiModule(path);
                break;
            default:
                throw new InvalidOperationException($"Unknown generated metadata fixture '{fileName}'.");
        }
    }

    private static CliResult RunJson(string fixture)
    {
        return CliRunner.Run("inspect", Paths.Get(fixture), "--json");
    }

    public void Dispose() => _temp.Dispose();
}
