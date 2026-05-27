using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

internal static partial class HarnessAssertions
{
    private const int SuccessExitCode = 0;
    private const int GateFailureExitCode = 1;
    private const string AssistedFix = "assisted_fix";
    private const string DiagnosticOnly = "diagnostic_only";
    private const string GuidedFix = "guided_fix";
    private const string ProfileName = "unity-bepinex";
    private static readonly IReadOnlyDictionary<string, Action<TempDir>> ScenarioAssertions =
        new Dictionary<string, Action<TempDir>>(StringComparer.Ordinal)
        {
            ["bep_valid_plugin"] = AssertBepValidPlugin,
            ["bep_helper_library"] = AssertBepHelperLibrary,
            ["bep_missing_hard_dep"] = AssertBepMissingHardDependency,
            ["bep_case_mismatch"] = AssertBepCaseMismatch,
            ["bep_version_mismatch"] = AssertBepVersionMismatch,
            ["bep_duplicate_guid"] = AssertBepDuplicateGuid,
            ["bep_unresolved_chain"] = AssertBepUnresolvedChain,
            ["plugin_invalid_artifact"] = AssertInvalidArtifact,
            ["generic_plugin_missing_ref"] = AssertGenericMissingReference,
            ["generic_plugin_conflict"] = AssertGenericConflict,
            ["generic_plugin_dup_provider"] = AssertGenericDuplicateProvider,
            ["strong_name_or_pinvoke_guided_fix"] = AssertPartialGuidedFix,
            ["header_auto_fix"] = AssertHeaderAutoFix,
        };

    public static void AssertScenario(string scenario, TempDir temp)
    {
        if (!ScenarioAssertions.TryGetValue(scenario, out Action<TempDir>? assertScenario))
            throw new InvalidOperationException($"Unknown v0.4 release harness scenario '{scenario}'.");

        assertScenario(temp);
    }

    public static void AssertBepValidPlugin(TempDir temp)
    {
        temp.Copy("F26_bep_meta.dll");

        JsonElement root = ScanUnityJson(temp);

        AssertProfile(root);
        Assert.Equal("pass", Gate(root, "integrity"));
        Assert.Equal("plugin", BepState(root, "F26_bep_meta.dll"));
        Assert.Empty(root.GetProperty("issues").EnumerateArray());
    }

    public static void AssertBepHelperLibrary(TempDir temp)
    {
        temp.CopyAll("F26_bep_meta.dll", "F01_compatible_anycpu.dll");

        JsonElement root = ScanUnityJson(temp);

        Assert.Equal("pass", Gate(root, "integrity"));
        Assert.Equal("helper_library", BepState(root, "F01_compatible_anycpu.dll"));
        Assert.Empty(root.GetProperty("issues").EnumerateArray());
    }

    public static void AssertBepMissingHardDependency(TempDir temp)
    {
        temp.Copy("F27_bep_miss.dll");

        JsonElement root = ScanUnityJson(temp);

        Assert.Equal("fail", Gate(root, "integrity"));
        Assert.Equal("blocked_missing_bep_dependency", BepState(root, "F27_bep_miss.dll"));
        AssertIssueContract(root, "bep_missing");
    }

    public static void AssertBepCaseMismatch(TempDir temp)
    {
        temp.CopyAll("F27_bep_miss.dll", "F31_bep_case.dll");

        JsonElement root = ScanUnityJson(temp);

        Assert.Equal("blocked_guid_case_mismatch", BepState(root, "F27_bep_miss.dll"));
        AssertIssueContract(root, "bep_casing");
    }

    public static void AssertBepVersionMismatch(TempDir temp)
    {
        temp.CopyAll("F27_bep_miss.dll", "F28_bep_need.dll");

        JsonElement root = ScanUnityJson(temp);
        JsonElement issue = AssertIssueContract(root, "bep_version_mismatch");
        JsonElement evidence = issue.GetProperty("evidence");

        Assert.Equal("blocked_bep_version_mismatch", BepState(root, "F27_bep_miss.dll"));
        Assert.Equal(">=2.0.0", evidence.GetProperty("declared_range").GetString());
        Assert.Equal("1.0.0", evidence.GetProperty("present_version").GetString());
    }

    public static void AssertBepDuplicateGuid(TempDir temp)
    {
        temp.Copy("F26_bep_meta.dll");
        CopyAs(temp, "F26_bep_meta.dll", "F26_bep_meta_copy.dll");

        JsonElement root = ScanUnityJson(temp);
        JsonElement issue = AssertIssueContract(root, "bep_dup_guid");

        Assert.Equal("test.meta", issue.GetProperty("subject").GetString());
        Assert.Equal(["F26_bep_meta.dll", "F26_bep_meta_copy.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
    }

    public static void AssertBepUnresolvedChain(TempDir temp)
    {
        temp.CopyAll("F20_closure_entry.dll", "F21_closure_mid.dll", "F22_closure_deep.dll");

        JsonElement root = ScanUnityJson(temp);
        JsonElement issue = AssertIssueContract(root, "plugin_unresolved_chain");
        JsonElement evidence = issue.GetProperty("evidence");

        Assert.Equal("risk_unresolved_assembly_chain", BepState(root, "F20_closure_entry.dll"));
        Assert.Equal("ClosureMissing.dll", evidence.GetProperty("missing_leaf").GetString());
        Assert.Equal(["ClosureMid.dll", "ClosureDeep.dll", "ClosureMissing.dll"], JsonAssert.StringArray(evidence.GetProperty("request_chain")));
    }

    public static void AssertInvalidArtifact(TempDir temp)
    {
        temp.CopyAll("F26_bep_meta.dll", "F05_reference_assembly.dll");

        JsonElement root = ScanUnityJson(temp);
        JsonElement result = ResultFor(root, "F05_reference_assembly.dll");

        Assert.Equal("invalid_artifact", BepState(root, "F05_reference_assembly.dll"));
        Assert.Equal("ref_assembly", result.GetProperty("reason_code").GetString());
        Assert.Equal(DiagnosticOnly, result.GetProperty("repair_class").GetString());
        Assert.NotEqual("fix", result.GetProperty("action").GetString());
    }

    public static void AssertGenericMissingReference(TempDir temp)
    {
        temp.Copy("F18_missing_refs.dll");

        JsonElement root = ScanJson(temp);

        Assert.Equal("fail", Gate(root, "integrity"));
        AssertIssueContract(root, "missing_ref");
    }

    public static void AssertGenericConflict(TempDir temp)
    {
        temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");

        AssertConflictGateFails(temp);
    }

    public static void AssertGenericDuplicateProvider(TempDir temp)
    {
        CopyAs(temp, "F01_compatible_anycpu.dll", "PluginA.dll");
        CopyAs(temp, "F01_compatible_anycpu.dll", "PluginB.dll");

        JsonElement root = ScanJson(temp);

        Assert.Equal("fail", Gate(root, "integrity"));
        AssertIssueContract(root, "dup_provider");
    }

    public static void AssertPartialGuidedFix(TempDir temp)
    {
        string path = temp.Copy("F03_x64_strongname.dll");
        JsonElement root = InspectJson(path);

        Assert.Equal("non_portable", root.GetProperty("reason_code").GetString());
        Assert.Equal(GuidedFix, root.GetProperty("repair_class").GetString());
        Assert.Contains("--force", root.GetProperty("repair_hint").GetString());
    }

    public static void AssertHeaderAutoFix(TempDir temp)
    {
        string path = temp.Copy("F02_x64only_managed.dll");

        CliResult result = CliRunner.Run("fix", path, "--apply");

        Assert.Equal(SuccessExitCode, result.ExitCode);
        Assert.Contains("Status:  PATCHED", result.Stdout);
        Assert.Contains("re-inspection passed", result.Stdout);
    }

    public static JsonElement AssertIssueContract(JsonElement root, string code)
    {
        JsonElement[] issues = [.. root.GetProperty("issues").EnumerateArray()
            .Where(issue => issue.GetProperty("code").GetString() == code)];

        Assert.NotEmpty(issues);
        foreach (JsonElement issue in issues)
        {
            Assert.Equal(AssistedFix, issue.GetProperty("repair_class").GetString());
            Assert.Equal("pefix scan <path> --json", issue.GetProperty("verify_command").GetString());
            Assert.NotEmpty(JsonAssert.StringArray(issue.GetProperty("next_steps")));
            Assert.NotEmpty(JsonAssert.StringArray(issue.GetProperty("unverified_risks")));
        }

        Assert.Contains(code, IssueCodes(root));
        return issues[0];
    }

    public static CliResult ScanUnityText(TempDir temp)
    {
        CliResult result = CliRunner.Run("scan", temp.DirPath, "--profile", ProfileName);

        Assert.Equal(SuccessExitCode, result.ExitCode);
        return result;
    }

    public static JsonElement ScanUnityJson(TempDir temp)
    {
        CliResult result = CliRunner.Run("scan", temp.DirPath, "--profile", ProfileName, "--json");

        Assert.Equal(SuccessExitCode, result.ExitCode);
        return JsonAssert.ParseObject(result.Stdout);
    }

    public static void AssertClosureGateFails(TempDir temp)
    {
        CliResult result = CliRunner.Run("closure", temp.DirPath, "--fail-on-unresolved");

        Assert.Equal(GateFailureExitCode, result.ExitCode);
    }

    public static void AssertConflictGateFails(TempDir temp)
    {
        CliResult result = CliRunner.Run("scan", temp.DirPath, "--json", "--fail-on-conflict");
        JsonElement root = JsonAssert.ParseObject(result.Stdout);

        Assert.Equal(GateFailureExitCode, result.ExitCode);
        Assert.Equal("fail", Gate(root, "version_conflict"));
        AssertIssueContract(root, "asm_conflict");
    }

    private static JsonElement ScanJson(TempDir temp)
    {
        CliResult result = CliRunner.Run("scan", temp.DirPath, "--json");

        Assert.Equal(SuccessExitCode, result.ExitCode);
        return JsonAssert.ParseObject(result.Stdout);
    }

    private static JsonElement InspectJson(string path)
    {
        CliResult result = CliRunner.Run("inspect", path, "--json");

        Assert.NotEqual(string.Empty, result.Stdout);
        return JsonAssert.ParseObject(result.Stdout);
    }

    private static string Gate(JsonElement root, string name)
    {
        return root.GetProperty("gate").GetProperty(name).GetString()
            ?? throw new InvalidOperationException($"Gate '{name}' was null.");
    }

    private static string[] IssueCodes(JsonElement root)
    {
        return JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes"));
    }

    private static JsonElement ResultFor(JsonElement root, string fileName)
    {
        return Assert.Single(
            root.GetProperty("results").EnumerateArray(),
            item => Path.GetFileName(item.GetProperty("path").GetString()) == fileName);
    }

    private static string BepState(JsonElement root, string fileName)
    {
        return ResultFor(root, fileName)
            .GetProperty("bepinex")
            .GetProperty("state")
            .GetString()
            ?? throw new InvalidOperationException("BepInEx state was null.");
    }

    private static void AssertProfile(JsonElement root)
    {
        JsonElement profiles = root.GetProperty("profiles");

        Assert.Equal(ProfileName, profiles.GetProperty("host").GetString());
        Assert.Equal("plugin-folder", profiles.GetProperty("artifact").GetString());
    }

    private static void CopyAs(TempDir temp, string fixture, string fileName)
    {
        File.Copy(Paths.Get(fixture), Path.Combine(temp.DirPath, fileName), overwrite: true);
    }
}
