using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BepClosureScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_ReportsPluginUnresolvedChain()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "plugin_unresolved_chain");

        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["missing_ref", "plugin_unresolved_chain"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("ClosureMissing", issue.GetProperty("subject").GetString());
        Assert.Equal(["F20_closure_entry.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        JsonElement evidence = issue.GetProperty("evidence");
        Assert.Equal("F20_closure_entry.dll", evidence.GetProperty("entry_file").GetString());
        Assert.Equal(["ClosureMid.dll", "ClosureDeep.dll", "ClosureMissing.dll"], JsonAssert.StringArray(evidence.GetProperty("request_chain")));
        Assert.Equal("ClosureMissing.dll", evidence.GetProperty("missing_leaf").GetString());
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Contains("ClosureEntry.dll loads ClosureMid.dll", issue.GetProperty("summary").GetString());
        Assert.Contains("ClosureDeep.dll needs ClosureMissing.dll", issue.GetProperty("summary").GetString());
        Assert.Equal("risk_unresolved_assembly_chain", BepState(root, "F20_closure_entry.dll"));
    }

    [Fact]
    public void Scan_TextShowsPluginUnresolvedChain()
    {
        _temp.CopyAll(
            "F20_closure_entry.dll",
            "F21_closure_mid.dll",
            "F22_closure_deep.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[plugin_unresolved_chain] ClosureMissing", result.Stdout);
        Assert.Contains("ClosureEntry.dll loads ClosureMid.dll", result.Stdout);
        Assert.Contains("ClosureDeep.dll needs ClosureMissing.dll", result.Stdout);
        Assert.Contains("repair: assisted_fix", result.Stdout);
        Assert.Contains("verify: pefix scan <path> --json", result.Stdout);
    }

    private static string BepState(JsonElement root, string fileName)
    {
        foreach (JsonElement result in root.GetProperty("results").EnumerateArray())
        {
            if (Path.GetFileName(result.GetProperty("path").GetString()) == fileName)
                return result.GetProperty("bepinex").GetProperty("state").GetString()
                    ?? throw new InvalidOperationException("BepInEx state was null.");
        }

        throw new InvalidOperationException($"Result for {fileName} was not found.");
    }

    public void Dispose() => _temp.Dispose();
}
