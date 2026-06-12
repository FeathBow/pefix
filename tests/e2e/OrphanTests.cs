using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class OrphanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Closure_OrphansListsUnreferencedConsumer()
    {
        // The consumer references the provider; nothing references the consumer.
        _temp.CopyAll("F36_member_consumer.dll", "F35_member_provider_full.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--orphans", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(["F36_member_consumer.dll"], JsonAssert.StringArray(root.GetProperty("orphans")));
    }

    [Fact]
    public void Closure_OrphansTextSection()
    {
        _temp.CopyAll("F36_member_consumer.dll", "F35_member_provider_full.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--orphans");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Unreferenced (1):", result.Stdout);
        Assert.Contains("- F36_member_consumer.dll", result.Stdout);
        Assert.Contains("advisory", result.Stdout);
    }

    [Fact]
    public void Closure_OrphansSkipsReflectionNamedTarget()
    {
        // The target is loaded only via a literal reflection string; it must
        // not be listed even though no AssemblyRef points at it.
        _temp.CopyAll("F38_reflection_present.dll", "F37_reflection_target.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--orphans", "--json");

        Assert.Equal(0, result.ExitCode);
        string[] orphans = JsonAssert.StringArray(JsonAssert.ParseObject(result.Stdout).GetProperty("orphans"));
        Assert.DoesNotContain("F37_reflection_target.dll", orphans);
        Assert.Contains("F38_reflection_present.dll", orphans);
    }

    [Fact]
    public void Closure_OrphansSkipsBepInExPluginEntry()
    {
        // The closure entry carries [BepInPlugin]: a chainloader root, not an
        // orphan. Mid and deep are referenced, so nothing is listed.
        _temp.CopyAll("F20_closure_entry.dll", "F21_closure_mid.dll", "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--orphans", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(JsonAssert.StringArray(JsonAssert.ParseObject(result.Stdout).GetProperty("orphans")));
    }

    [Fact]
    public void Closure_WithoutOrphansKeepsJsonShape()
    {
        _temp.CopyAll("F20_closure_entry.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(JsonAssert.ParseObject(result.Stdout).TryGetProperty("orphans", out _));
    }

    public void Dispose() => _temp.Dispose();
}
