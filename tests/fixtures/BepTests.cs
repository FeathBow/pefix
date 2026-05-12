using System.Text.Json;

namespace PeFix.Tests;

public sealed class BepTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_Json_ShowsPluginMetaThroughCli()
    {
        _temp.Copy("F26_bep_meta.dll");
        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        JsonElement plugin = JsonAssert.ParseObject(result.Stdout)
            .GetProperty("results")[0]
            .GetProperty("bepinex")
            .GetProperty("plugins")[0];
        Assert.Equal("test.meta", plugin.GetProperty("guid").GetString());
        Assert.Equal("Meta Plugin", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
