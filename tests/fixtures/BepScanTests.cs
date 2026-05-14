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
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");
        JsonElement issue = Assert.Single(root.GetProperty("issues").EnumerateArray());
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal(["bep_missing"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal("bep_missing", issue.GetProperty("code").GetString());
        Assert.Equal("need.hard", issue.GetProperty("subject").GetString());
        Assert.Equal(["F27_bep_miss.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal(
            ["Install the missing BepInEx plugin dependency into the scanned plugins directory."],
            JsonAssert.StringArray(issue.GetProperty("next_steps")));
        Assert.Equal(1, summary.GetProperty("by_issue").GetProperty("bep_missing").GetInt32());
    }

    [Fact]
    public void Scan_Json_MarksHardDependencyPresent()
    {
        Copy("F27_bep_miss.dll");
        Copy("F28_bep_need.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.hard");

        Assert.True(dep.GetProperty("hard").GetBoolean());
        Assert.True(dep.GetProperty("present").GetBoolean());
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal([], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Empty(root.GetProperty("issues").EnumerateArray());
        Assert.False(root.GetProperty("summary").GetProperty("by_issue").TryGetProperty("bep_missing", out _));
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
        Assert.Equal(["bep_missing"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
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
        foreach (JsonElement dep in AllDeps(root))
        {
            if (string.Equals(dep.GetProperty("guid").GetString(), guid, StringComparison.Ordinal))
                return dep;
        }

        throw new InvalidOperationException($"BepInEx dependency '{guid}' was not found.");
    }

    private static IEnumerable<JsonElement> AllDeps(JsonElement root)
    {
        foreach (JsonElement plugin in AllPlugins(root))
        {
            foreach (JsonElement dep in plugin.GetProperty("deps").EnumerateArray())
                yield return dep;
        }
    }

    private static IEnumerable<JsonElement> AllPlugins(JsonElement root)
    {
        foreach (JsonElement result in root.GetProperty("results").EnumerateArray())
        {
            if (!result.TryGetProperty("bepinex", out JsonElement bep))
                continue;

            foreach (JsonElement plugin in bep.GetProperty("plugins").EnumerateArray())
                yield return plugin;
        }
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
