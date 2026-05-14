using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ClosureE2E : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void TextOutput()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll",
            "F23_closure_cycle_a.dll",
            "F24_closure_cycle_b.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("UNRESOLVED", result.Stdout);
        Assert.Contains("ClosureMissing", result.Stdout);
        Assert.Contains("MISSING", result.Stdout);
        Assert.Contains("Cycle chains:", result.Stdout);
        Assert.Contains("ClosureCycleA", result.Stdout);
        Assert.Contains("ClosureCycleB", result.Stdout);
        Assert.Contains("CYCLE", result.Stdout);
        Assert.Contains("entry assemblies", result.Stdout);
        Assert.Contains("transitive references", result.Stdout);
        Assert.Contains("→ ClosureMid.dll v1.0.0.0", result.Stdout);
        Assert.Contains("→ ClosureDeep.dll v1.0.0.0", result.Stdout);
        Assert.Contains("→ ClosureMissing.dll v1.0.0.0  [MISSING]", result.Stdout);
        Assert.Contains("[resolved]", result.Stdout);
    }

    [Fact]
    public void JsonContract()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.True(root.TryGetProperty("directory", out _));
        Assert.True(root.TryGetProperty("entry_assemblies", out _));
        Assert.True(root.TryGetProperty("unresolved_chains", out _));
        Assert.True(root.TryGetProperty("cycle_chains", out _));
        Assert.True(root.TryGetProperty("total_refs_walked", out _));
        Assert.True(root.TryGetProperty("framework_leaves", out _));

        JsonElement unresolved = root.GetProperty("unresolved_chains");
        JsonElement chain = Assert.Single(
            unresolved.EnumerateArray(),
            item => item.GetProperty("entry").GetString() == "ClosureEntry");
        JsonElement segments = chain.GetProperty("segments");
        Assert.Equal(3, segments.GetArrayLength());
        AssertSeg(segments[0], "ClosureMid", "resolved");
        AssertSeg(segments[1], "ClosureDeep", "resolved");
        AssertSeg(segments[2], "ClosureMissing", "unresolved");
    }

    [Fact]
    public void FailOnFlag()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--fail-on-unresolved");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void FailOnFlag_JsonStillWritesReport()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--json", "--fail-on-unresolved");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        JsonElement unresolved = root.GetProperty("unresolved_chains");
        Assert.Contains(
            unresolved.EnumerateArray(),
            item => item.GetProperty("entry").GetString() == "ClosureEntry");
    }

    [Fact]
    public void FailOnFlag_AllResolved()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--fail-on-unresolved");

        Assert.Equal(0, result.ExitCode);
    }

    private static void AssertSeg(JsonElement segment, string name, string kind)
    {
        Assert.Equal(name, segment.GetProperty("name").GetString());
        Assert.Equal("1.0.0.0", segment.GetProperty("version").GetString());
        Assert.Equal(kind, segment.GetProperty("kind").GetString());
    }

    public void Dispose() => _temp.Dispose();
}
