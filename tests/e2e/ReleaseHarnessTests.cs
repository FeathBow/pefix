using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ReleaseHarnessTests
{
    [Fact]
    public void ReleaseMatrix_MapsR1ToR17IntoReleaseGate()
    {
        HarnessRequirement[] releaseGateRequirements = ReleaseHarnessMatrix.GateRequirements();
        HarnessRequirement[] nonGateRequirements = ReleaseHarnessMatrix.NonGateRequirements();

        Assert.Equal(ReleaseHarnessMatrix.TotalRequirementCount, ReleaseHarnessMatrix.Requirements.Length);
        Assert.Equal(ReleaseHarnessMatrix.GateRequirementCount, releaseGateRequirements.Length);
        Assert.All(releaseGateRequirements.Take(12), item => Assert.Equal("Supported", item.State));
        Assert.Equal("Partial", Assert.Single(releaseGateRequirements, item => item.Id == "R13").State);
        Assert.Equal("Deferred", Assert.Single(nonGateRequirements, item => item.Id == "R14").State);
        Assert.All(nonGateRequirements.Where(item => item.Id != "R14"), item => Assert.Equal("No-fit", item.State));
        Assert.All(releaseGateRequirements, item => Assert.False(string.IsNullOrWhiteSpace(item.Scenario)));
        Assert.Equal(["R14", "R15", "R16", "R17"], nonGateRequirements.Select(item => item.Id).ToArray());
        Assert.All(nonGateRequirements, item => Assert.True(string.IsNullOrWhiteSpace(item.Scenario)));
    }

    [Theory]
    [MemberData(nameof(GateScenarioIds))]
    public void FixtureScenario_AnswersReleaseGate(string scenario)
    {
        using var temp = new TempDir();

        if (!ReleaseHarnessMatrix.ScenarioAssertions.TryGetValue(scenario, out Action<TempDir>? assertScenario))
            throw new InvalidOperationException($"Unknown v0.4 release harness scenario '{scenario}'.");

        assertScenario(temp);
    }

    [Fact]
    public void CliGateCommands_HaveFixtureBackedAdapters()
    {
        using var scanText = new TempDir();
        scanText.Copy("F27_bep_miss.dll");
        CliResult scanTextResult = HarnessAssertions.ScanUnityText(scanText);
        Assert.Contains("Issues", scanTextResult.Stdout);

        using var scanJson = new TempDir();
        scanJson.Copy("F27_bep_miss.dll");
        JsonElement scanJsonRoot = HarnessAssertions.ScanUnityJson(scanJson);
        HarnessAssertions.AssertIssueContract(scanJsonRoot, "bep_missing");

        using var publishGate = new TempDir();
        HarnessAssertions.AssertPublishGateFails(publishGate);

        using var closure = new TempDir();
        closure.CopyAll("F20_closure_entry.dll", "F21_closure_mid.dll", "F22_closure_deep.dll");
        HarnessAssertions.AssertClosureGateFails(closure);

        using var conflict = new TempDir();
        conflict.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        HarnessAssertions.AssertConflictGateFails(conflict);

        using var fix = new TempDir();
        HarnessAssertions.AssertHeaderAutoFix(fix);
    }

    public static TheoryData<string> GateScenarioIds() => ReleaseHarnessMatrix.GateScenarioIds();
}
