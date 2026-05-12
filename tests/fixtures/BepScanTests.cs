using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BepScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_Json_ShowsPluginMeta()
    {
        Copy("F26_bep_meta.dll");
        JsonElement plugin = OnePlugin(Scan(), "test.meta");

        Assert.Equal("test.meta", plugin.GetProperty("guid").GetString());
        Assert.Equal("Meta Plugin", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    [Fact]
    public void Scan_Json_MarksHardMissingDependency()
    {
        JsonElement root = Scan("F27_bep_miss.dll");
        JsonElement dep = OneDep(root, "need.hard");

        Assert.True(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(">=2.0.0", dep.GetProperty("range").GetString());
        Assert.False(dep.GetProperty("present").GetBoolean());
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal("bep_missing", root.GetProperty("gate").GetProperty("issue_codes")[0].GetString());
    }

    [Fact]
    public void Scan_Json_MarksHardDependencyPresent()
    {
        Copy("F27_bep_miss.dll");
        Copy("F28_bep_need.dll");
        JsonElement dep = OneDep(Scan(), "need.hard");

        Assert.True(dep.GetProperty("hard").GetBoolean());
        Assert.True(dep.GetProperty("present").GetBoolean());
    }

    [Fact]
    public void Scan_Json_MarksCaseMismatchDependency()
    {
        Copy("F27_bep_miss.dll");
        JsonElement root = Scan("F31_bep_case.dll");
        JsonElement dep = OneDep(root, "need.hard");

        Assert.False(dep.GetProperty("present").GetBoolean());
        Assert.True(dep.GetProperty("case_mismatch").GetBoolean());
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Contains("bep_missing", root.GetProperty("gate").GetProperty("issue_codes").EnumerateArray().Select(code => code.GetString()));
    }

    [Fact]
    public void Scan_Json_MarksSoftDependencyMissingWithoutFailingGate()
    {
        Copy("F29_bep_soft.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.soft");

        Assert.False(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(">=1.0.0", dep.GetProperty("range").GetString());
        Assert.False(dep.GetProperty("present").GetBoolean());
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
    }

    [Fact]
    public void Scan_Json_MarksSoftDependencyFromFixedEnumFlags()
    {
        Copy("F32_bep_flag.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.flag");

        Assert.False(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dep.GetProperty("range").ValueKind);
        Assert.False(dep.GetProperty("present").GetBoolean());
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
    }

    [Fact]
    public void Scan_Json_SupportsAttributeSuffixForm()
    {
        JsonElement plugin = OnePlugin(Scan("F30_bep_main.dll"), "com.pefix.main");

        Assert.Equal("Pefix Main", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    [Fact]
    public void Scan_Text_ShowsBepInExTriageBlock()
    {
        Copy("F27_bep_miss.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("BepInEx deps (1):", result.Stdout);
        Assert.Contains("test.miss requires BepInEx plugin need.hard", result.Stdout);
        Assert.Contains("Install the missing BepInEx plugin dependency", result.Stdout);
    }

    private JsonElement Scan(string name)
    {
        Copy(name);
        return Scan();
    }

    private JsonElement Scan()
    {
        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        return JsonAssert.ParseObject(result.Stdout);
    }

    private void Copy(string name)
    {
        string dll = Paths.Get(name);
        File.Copy(dll, Path.Combine(_temp.DirPath, name), overwrite: true);
    }

    private static JsonElement OnePlugin(JsonElement root, string guid)
    {
        JsonElement plugins = root
            .GetProperty("results")[0]
            .GetProperty("bepinex")
            .GetProperty("plugins");
        return JsonAssert.SingleBy(plugins, "guid", guid);
    }

    private static JsonElement OneDep(JsonElement root, string guid)
    {
        foreach (JsonElement result in root.GetProperty("results").EnumerateArray())
        {
            if (!result.TryGetProperty("bepinex", out JsonElement bep))
                continue;

            foreach (JsonElement plugin in bep.GetProperty("plugins").EnumerateArray())
            {
                JsonElement deps = plugin.GetProperty("deps");
                foreach (JsonElement dep in deps.EnumerateArray())
                {
                    if (string.Equals(dep.GetProperty("guid").GetString(), guid, StringComparison.Ordinal))
                        return dep;
                }
            }
        }

        throw new InvalidOperationException($"BepInEx dependency '{guid}' was not found.");
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
