using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ReleaseHarnessTests
{
    private const int RequirementCount = 17;
    private const int ReleaseGateRequirementCount = 13;

    [Fact]
    public void ReleaseMatrix_MapsR1ToR17IntoReleaseGate()
    {
        Assert.Equal(RequirementCount, Requirements.Length);

        ReleaseRequirement[] releaseGate = [.. Requirements.Where(item => item.InReleaseGate)];
        ReleaseRequirement[] nonGate = [.. Requirements.Where(item => !item.InReleaseGate)];

        Assert.Equal(ReleaseGateRequirementCount, releaseGate.Length);
        Assert.All(releaseGate, item => Assert.False(string.IsNullOrWhiteSpace(item.Scenario)));
        Assert.Equal(["R14", "R15", "R16", "R17"], nonGate.Select(item => item.Id).ToArray());
        Assert.All(nonGate, item => Assert.True(string.IsNullOrWhiteSpace(item.Scenario)));
    }

    [Theory]
    [MemberData(nameof(GateScenarios))]
    public void FixtureScenario_AnswersReleaseGate(string scenario)
    {
        using var temp = new TempDir();

        HarnessAssertions.AssertScenario(scenario, temp);
    }

    [Fact]
    public void CliGateCommands_HaveFixtureBackedAdapters()
    {
        using var scanText = new TempDir();
        scanText.Copy("F27_bep_miss.dll");
        CliResult scanTextResult = HarnessAssertions.ScanUnityText(scanText);
        Assert.Contains("Blocking Issues", scanTextResult.Stdout);

        using var scanJson = new TempDir();
        scanJson.Copy("F27_bep_miss.dll");
        JsonElement scanJsonRoot = HarnessAssertions.ScanUnityJson(scanJson);
        HarnessAssertions.AssertIssueContract(scanJsonRoot, "bep_missing");

        using var closure = new TempDir();
        closure.CopyAll("F20_closure_entry.dll", "F21_closure_mid.dll", "F22_closure_deep.dll");
        HarnessAssertions.AssertClosureGateFails(closure);

        using var conflict = new TempDir();
        conflict.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        HarnessAssertions.AssertConflictGateFails(conflict);

        using var fix = new TempDir();
        HarnessAssertions.AssertHeaderAutoFix(fix);
    }

    public static TheoryData<string> GateScenarios => new()
    {
        "bep_valid_plugin",
        "bep_helper_library",
        "bep_missing_hard_dep",
        "bep_case_mismatch",
        "bep_version_mismatch",
        "bep_duplicate_guid",
        "bep_unresolved_chain",
        "plugin_invalid_artifact",
        "generic_plugin_missing_ref",
        "generic_plugin_conflict",
        "generic_plugin_dup_provider",
        "strong_name_or_pinvoke_guided_fix",
        "header_auto_fix",
    };

    private static readonly ReleaseRequirement[] Requirements =
    [
        Requirement("R1", "Supported", true, "bep_missing_hard_dep"),
        Requirement("R2", "Supported", true, "bep_valid_plugin"),
        Requirement("R3", "Supported", true, "bep_missing_hard_dep"),
        Requirement("R4", "Supported", true, "bep_case_mismatch"),
        Requirement("R5", "Supported", true, "bep_version_mismatch"),
        Requirement("R6", "Supported", true, "bep_duplicate_guid"),
        Requirement("R7", "Supported", true, "bep_unresolved_chain"),
        Requirement("R8", "Supported", true, "plugin_invalid_artifact"),
        Requirement("R9", "Supported", true, "generic_plugin_missing_ref"),
        Requirement("R10", "Supported", true, "generic_plugin_dup_provider"),
        Requirement("R11", "Supported", true, "generic_plugin_conflict"),
        Requirement("R12", "Supported", true, "header_auto_fix"),
        Requirement("R13", "Partial", true, "strong_name_or_pinvoke_guided_fix"),
        Requirement("R14", "Deferred", false),
        Requirement("R15", "No-fit", false),
        Requirement("R16", "No-fit", false),
        Requirement("R17", "No-fit", false),
    ];

    private static ReleaseRequirement Requirement(
        string id,
        string state,
        bool inReleaseGate,
        string? scenario = null)
    {
        return new ReleaseRequirement
        {
            Id = id,
            State = state,
            InReleaseGate = inReleaseGate,
            Scenario = scenario
        };
    }

    private sealed class ReleaseRequirement
    {
        public required string Id { get; init; }

        public required string State { get; init; }

        public required bool InReleaseGate { get; init; }

        public string? Scenario { get; init; }
    }
}
