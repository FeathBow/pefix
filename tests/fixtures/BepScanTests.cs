using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BepScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_ShowsPluginMeta()
    {
        Copy("F26_bep_meta.dll");
        JsonElement plugin = OnePlugin(Scan(), "test.meta");

        Assert.Equal("test.meta", plugin.GetProperty("guid").GetString());
        Assert.Equal("Meta Plugin", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
    }

    [Fact]
    public void Scan_ShowsBepInExExplainStates()
    {
        Copy("F26_bep_meta.dll");
        Copy("F01_compatible_anycpu.dll");
        Copy("F05_reference_assembly.dll");
        JsonElement root = Scan();

        Assert.Equal("plugin", BepState(root, "F26_bep_meta.dll"));
        Assert.Equal("helper_library", BepState(root, "F01_compatible_anycpu.dll"));
        Assert.Equal("invalid_artifact", BepState(root, "F05_reference_assembly.dll"));
    }

    [Fact]
    public void Scan_MarksHardMissingDependency()
    {
        JsonElement root = Scan("F27_bep_miss.dll");
        JsonElement dep = OneDep(root, "need.hard");

        Assert.True(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(">=2.0.0", dep.GetProperty("range").GetString());
        AssertProviderAbsent(dep);
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");
        JsonElement issue = Assert.Single(root.GetProperty("issues").EnumerateArray());
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal(["bep_missing"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal("bep_missing", issue.GetProperty("code").GetString());
        Assert.Equal("need.hard", issue.GetProperty("subject").GetString());
        Assert.Equal(["F27_bep_miss.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal("blocked_missing_bep_dependency", BepState(root, "F27_bep_miss.dll"));
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Contains("Install or restore the missing BepInEx plugin dependency", issue.GetProperty("repair_hint").GetString());
        Assert.Equal("pefix scan <path> --json", issue.GetProperty("verify_command").GetString());
        Assert.Contains("chainloader success", JsonAssert.StringArray(issue.GetProperty("unverified_risks"))[0]);
        Assert.Equal(
            ["Install the missing BepInEx plugin dependency into the scanned plugins directory."],
            JsonAssert.StringArray(issue.GetProperty("next_steps")));
        Assert.Equal(1, summary.GetProperty("by_issue").GetProperty("bep_missing").GetInt32());
    }

    [Fact]
    public void Scan_MarksHardDependencyProviderPresentButVersionUnsatisfied()
    {
        Copy("F27_bep_miss.dll");
        Copy("F28_bep_need.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.hard");

        Assert.True(dep.GetProperty("hard").GetBoolean());
        AssertProviderPresent(dep);
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["bep_version_mismatch"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("blocked_bep_version_mismatch", BepState(root, "F27_bep_miss.dll"));
        Assert.False(root.GetProperty("summary").GetProperty("by_issue").TryGetProperty("bep_missing", out _));
    }

    [Fact]
    public void Scan_MarksCaseMismatchDependency()
    {
        Copy("F27_bep_miss.dll");
        JsonElement root = Scan("F31_bep_case.dll");
        JsonElement dep = OneDep(root, "need.hard");

        AssertProviderAbsent(dep);
        Assert.True(dep.GetProperty("case_mismatch").GetBoolean());
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "bep_casing");
        Assert.Contains("Fix the plugin GUID casing", issue.GetProperty("repair_hint").GetString());
        Assert.Contains("Fix the plugin GUID casing", JsonAssert.StringArray(issue.GetProperty("next_steps"))[0]);
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Equal("pefix scan <path> --json", issue.GetProperty("verify_command").GetString());
        Assert.Contains("chainloader success", JsonAssert.StringArray(issue.GetProperty("unverified_risks"))[0]);
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["bep_casing"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("bep_casing", issue.GetProperty("code").GetString());
        Assert.Equal("blocked_guid_case_mismatch", BepState(root, "F27_bep_miss.dll"));
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_issue").GetProperty("bep_casing").GetInt32());
    }

    [Fact]
    public void Scan_MarksDuplicateBepInExGuid()
    {
        Copy("F26_bep_meta.dll");
        File.Copy(Paths.Get("F26_bep_meta.dll"), Path.Combine(_temp.DirPath, "F26_bep_meta_copy.dll"), overwrite: true);

        JsonElement root = Scan();
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "bep_dup_guid");

        Assert.Equal("bep_dup_guid", issue.GetProperty("code").GetString());
        Assert.Equal("test.meta", issue.GetProperty("subject").GetString());
        Assert.Equal(["F26_bep_meta.dll", "F26_bep_meta_copy.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal(
            ["F26_bep_meta.dll", "F26_bep_meta_copy.dll"],
            JsonAssert.StringArray(issue.GetProperty("evidence").GetProperty("provider_files")));
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Contains("Keep one BepInEx plugin", issue.GetProperty("repair_hint").GetString());
        Assert.Equal(["bep_dup_guid", "dup_provider"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    [Fact]
    public void Scan_MarksBepInExVersionMismatch()
    {
        Copy("F27_bep_miss.dll");
        Copy("F28_bep_need.dll");

        JsonElement root = Scan();
        JsonElement issue = Assert.Single(root.GetProperty("issues").EnumerateArray());

        Assert.Equal("bep_version_mismatch", issue.GetProperty("code").GetString());
        Assert.Equal("need.hard", issue.GetProperty("subject").GetString());
        Assert.Equal(["F27_bep_miss.dll", "F28_bep_need.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        JsonElement evidence = issue.GetProperty("evidence");
        Assert.Equal(">=2.0.0", evidence.GetProperty("declared_range").GetString());
        Assert.Equal("1.0.0", evidence.GetProperty("present_version").GetString());
        Assert.Equal(["F28_bep_need.dll"], JsonAssert.StringArray(evidence.GetProperty("provider_files")));
        Assert.Equal("blocked_bep_version_mismatch", BepState(root, "F27_bep_miss.dll"));
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Contains("Install a BepInEx plugin dependency version", issue.GetProperty("repair_hint").GetString());
        Assert.Equal(["bep_version_mismatch"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    [Fact]
    public void Scan_MarksSoftDependencyMissingWithoutFailingGate()
    {
        Copy("F29_bep_soft.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.soft");

        Assert.False(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(">=1.0.0", dep.GetProperty("range").GetString());
        AssertProviderAbsent(dep);
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
    }

    [Fact]
    public void Scan_MarksSoftDependencyFromFixedEnumFlags()
    {
        Copy("F32_bep_flag.dll");
        JsonElement root = Scan();
        JsonElement dep = OneDep(root, "need.flag");

        Assert.False(dep.GetProperty("hard").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dep.GetProperty("range").ValueKind);
        AssertProviderAbsent(dep);
        Assert.Equal("pass", root.GetProperty("gate").GetProperty("integrity").GetString());
    }

    [Fact]
    public void Scan_SupportsAttributeSuffixForm()
    {
        JsonElement plugin = OnePlugin(Scan("F30_bep_main.dll"), "com.pefix.main");

        Assert.Equal("Pefix Main", plugin.GetProperty("name").GetString());
        Assert.Equal("1.2.3", plugin.GetProperty("version").GetString());
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

    private static void AssertProviderPresent(JsonElement dependency)
    {
        Assert.True(dependency.GetProperty("present").GetBoolean());
    }

    private static void AssertProviderAbsent(JsonElement dependency)
    {
        Assert.False(dependency.GetProperty("present").GetBoolean());
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

    private static string BepState(JsonElement root, string fileName)
    {
        JsonElement result = Assert.Single(
            root.GetProperty("results").EnumerateArray(),
            item => Path.GetFileName(item.GetProperty("path").GetString()) == fileName);
        return result.GetProperty("bepinex").GetProperty("state").GetString()
            ?? throw new InvalidOperationException("BepInEx state was null.");
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
