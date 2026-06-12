using System.Linq;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class Il2CppApiTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_EmitPluginOnIl2CppProfileFailsGate()
    {
        _temp.CopyAll("F57_emit_plugin.dll");

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "unity-bepinex6-il2cpp", "--json", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "bep_il2cpp_api");
        Assert.Equal("System.Reflection.Emit", issue.GetProperty("subject").GetString());
        Assert.Contains("IL2CPP runtime does not support", issue.GetProperty("summary").GetString());
        Assert.Contains(
            "bep_il2cpp_api",
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    [Fact]
    public void Scan_EmitPluginOnMonoProfileStaysSilent()
    {
        _temp.CopyAll("F57_emit_plugin.dll");

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "unity-bepinex5", "--json", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "bep_il2cpp_api");
    }

    [Fact]
    public void Scan_EmitPluginWithoutKnownLoaderStaysSilent()
    {
        _temp.CopyAll("F57_emit_plugin.dll");

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "unity-bepinex", "--json", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "bep_il2cpp_api");
    }

    public void Dispose() => _temp.Dispose();
}
