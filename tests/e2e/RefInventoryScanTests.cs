using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class RefInventoryScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_ReferencesJsonIncludesAllStatusesAndMatchesIssues()
    {
        CopyReferenceInventoryCase();

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--references");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement references = root.GetProperty("references");
        AssertStatus(references, "CompatibleAnyCpu", "F17_conflict.dll", "version_conflict");
        AssertStatus(references, "ClosureDeep", "F21_closure_mid.dll", "present");
        AssertStatus(references, "ClosureMissing", "F22_closure_deep.dll", "missing");
        AssertStatus(references, "System.Runtime", "F01_compatible_anycpu.dll", "host_provided");
        AssertIssueConsistency(root);
    }

    [Fact]
    public void Scan_ReferencesFlagPreservesDefaultOutputShape()
    {
        _temp.CopyAll("F18_missing_refs.dll");

        CliResult text = CliRunner.Run("scan", _temp.DirPath);
        CliResult json = CliRunner.Run("scan", _temp.DirPath, "--json");
        CliResult textRefs = CliRunner.Run("scan", _temp.DirPath, "--references");
        CliResult jsonRefs = CliRunner.Run("scan", _temp.DirPath, "--json", "--references");

        Assert.DoesNotContain("References (", text.Stdout);
        Assert.False(JsonAssert.ParseObject(json.Stdout).TryGetProperty("references", out _));
        Assert.Contains("References (", textRefs.Stdout);
        Assert.True(JsonAssert.ParseObject(jsonRefs.Stdout).TryGetProperty("references", out _));
    }

    public void Dispose() => _temp.Dispose();

    private void CopyReferenceInventoryCase()
    {
        _temp.CopyAll(
            "F01_compatible_anycpu.dll",
            "F17_conflict.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");
    }

    private static void AssertIssueConsistency(JsonElement root)
    {
        JsonElement references = root.GetProperty("references");
        JsonElement conflict = JsonAssert.SingleBy(root.GetProperty("conflicts"), "assembly", "CompatibleAnyCpu");
        JsonElement missing = JsonAssert.SingleBy(root.GetProperty("missing_refs"), "assembly", "ClosureMissing");
        Assert.Equal("version_conflict", ConsumerStatus(references, conflict.GetProperty("assembly").GetString()!, "F17_conflict.dll"));
        Assert.Equal("missing", ConsumerStatus(references, missing.GetProperty("assembly").GetString()!, "F22_closure_deep.dll"));
    }

    private static void AssertStatus(
        JsonElement references,
        string name,
        string consumer,
        string status)
    {
        Assert.Equal(status, ConsumerStatus(references, name, consumer));
    }

    private static string ConsumerStatus(
        JsonElement references,
        string name,
        string consumer)
    {
        JsonElement reference = JsonAssert.SingleBy(references, "name", name);
        JsonElement item = JsonAssert.SingleBy(reference.GetProperty("consumers"), "consumer", consumer);
        return item.GetProperty("status").GetString() ?? throw new JsonException("Missing status.");
    }
}
