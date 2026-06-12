namespace PeFix.Tests;

internal static class ReleaseHarnessMatrix
{
    private const string RequirementPrefix = "R";
    internal const int TotalRequirementCount = 17;
    internal const int GateRequirementCount = 13;

    private static readonly GateScenario[] GateScenarios =
    [
        new("bep_missing_hard_dep", "Supported", HarnessAssertions.AssertBepMissingHardDependency, ["R1", "R3"]),
        new("bep_valid_plugin", "Supported", HarnessAssertions.AssertBepValidPlugin, ["R2"]),
        new("bep_case_mismatch", "Supported", HarnessAssertions.AssertBepCaseMismatch, ["R4"]),
        new("bep_version_mismatch", "Supported", HarnessAssertions.AssertBepVersionMismatch, ["R5"]),
        new("bep_duplicate_guid", "Supported", HarnessAssertions.AssertBepDuplicateGuid, ["R6"]),
        new("bep_unresolved_chain", "Supported", HarnessAssertions.AssertBepUnresolvedChain, ["R7"]),
        new("plugin_invalid_artifact", "Supported", HarnessAssertions.AssertInvalidArtifact, ["R8"]),
        new("generic_plugin_missing_ref", "Supported", HarnessAssertions.AssertGenericMissingReference, ["R9"]),
        new("generic_plugin_dup_provider", "Supported", HarnessAssertions.AssertGenericDuplicateProvider, ["R10"]),
        new("generic_plugin_conflict", "Supported", HarnessAssertions.AssertGenericConflict, ["R11"]),
        new("header_auto_fix", "Supported", HarnessAssertions.AssertHeaderAutoFix, ["R12"]),
        new("strong_name_or_pinvoke_guided_fix", "Partial", HarnessAssertions.AssertPartialGuidedFix, ["R13"]),
    ];

    private static readonly HarnessScenario[] ExtraScenarios =
    [
        new("bep_helper_library", HarnessAssertions.AssertBepHelperLibrary),
    ];

    private static readonly HarnessRequirement[] NonGateRequirementRows =
    [
        new("R14", "Deferred", false),
        new("R15", "No-fit", false),
        new("R16", "No-fit", false),
        new("R17", "No-fit", false),
    ];

    internal static readonly HarnessRequirement[] Requirements = BuildRequirements();

    internal static readonly IReadOnlyDictionary<string, Action<TempDir>> ScenarioAssertions = BuildScenarioAssertions();

    internal static HarnessRequirement[] GateRequirements()
    {
        return [.. Requirements.Where(item => item.InReleaseGate)];
    }

    internal static HarnessRequirement[] NonGateRequirements()
    {
        return [.. Requirements.Where(item => !item.InReleaseGate)];
    }

    internal static TheoryData<string> GateScenarioIds()
    {
        TheoryData<string> ids = [];
        foreach (GateScenario scenario in GateScenarios.OrderBy(item => item.FirstRequirementNumber))
            ids.Add(scenario.Id);

        return ids;
    }

    private static HarnessRequirement[] BuildRequirements()
    {
        IEnumerable<HarnessRequirement> gateRequirements = GateScenarios.SelectMany(
            scenario => scenario.RequirementIds.Select(
                id => new HarnessRequirement(id, scenario.State, true, scenario.Id)));

        return [.. gateRequirements.Concat(NonGateRequirementRows).OrderBy(item => RequirementNumber(item.Id))];
    }

    private static Dictionary<string, Action<TempDir>> BuildScenarioAssertions()
    {
        Dictionary<string, Action<TempDir>> assertions = new(StringComparer.Ordinal);
        foreach (GateScenario scenario in GateScenarios.OrderBy(item => item.FirstRequirementNumber))
            AddAssertion(assertions, scenario.Id, scenario.Assertion);

        foreach (HarnessScenario scenario in ExtraScenarios)
            AddAssertion(assertions, scenario.Id, scenario.Assertion);

        return assertions;
    }

    private static void AddAssertion(
        Dictionary<string, Action<TempDir>> assertions,
        string scenario,
        Action<TempDir> assertion)
    {
        if (!assertions.TryAdd(scenario, assertion))
            throw new InvalidOperationException($"Release scenario '{scenario}' is duplicated.");
    }

    private static int RequirementNumber(string id)
    {
        if (!id.StartsWith(RequirementPrefix, StringComparison.Ordinal) ||
            !int.TryParse(id[RequirementPrefix.Length..], out int number))
            throw new InvalidOperationException($"Release requirement '{id}' is not a valid R-number.");

        return number;
    }

    private sealed record GateScenario(
        string Id,
        string State,
        Action<TempDir> Assertion,
        string[] RequirementIds)
    {
        internal int FirstRequirementNumber => RequirementIds.Select(RequirementNumber).Min();
    }

    private sealed record HarnessScenario(
        string Id,
        Action<TempDir> Assertion);
}
