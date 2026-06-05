namespace PeFix.Tests;

internal readonly record struct HarnessRequirement(
    string Id,
    string State,
    bool InReleaseGate,
    string? Scenario = null);
