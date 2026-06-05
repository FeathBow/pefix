using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class BepLoaderMismatchScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_FlagsMixedLoaderGenerationAndFlavor()
    {
        // F26 is a BepInEx 5 (Mono) plugin; F33 is a BepInEx 6 (IL2CPP) plugin.
        // A single host loads only one, so this folder cannot fully load.
        _temp.CopyAll("F26_bep_meta.dll", "F33_bep_il2cpp.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex", "--json");

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "bep_loader_mismatch");

        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Contains("bep_loader_mismatch", JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString());
        Assert.Equal("loader target", issue.GetProperty("subject").GetString());
        Assert.Equal(["F26_bep_meta.dll", "F33_bep_il2cpp.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal(JsonValueKind.Null, issue.GetProperty("evidence").ValueKind);
        Assert.NotEmpty(JsonAssert.StringArray(issue.GetProperty("next_steps")));
        Assert.Equal("pefix scan <path> --json", issue.GetProperty("verify_command").GetString());
        Assert.NotEmpty(JsonAssert.StringArray(issue.GetProperty("unverified_risks")));
        Assert.Contains("IL2CPP", issue.GetProperty("summary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("blocked_loader_mismatch", BepState(root, "F33_bep_il2cpp.dll"));

        Assert.Equal("bepinex6", LoaderField(root, "F33_bep_il2cpp.dll", "loader_generation"));
        Assert.Equal("il2cpp", LoaderField(root, "F33_bep_il2cpp.dll", "loader_flavor"));
        Assert.Equal("bepinex5", LoaderField(root, "F26_bep_meta.dll", "loader_generation"));
        Assert.Equal("mono", LoaderField(root, "F26_bep_meta.dll", "loader_flavor"));
        Assert.Equal("BepInEx.Unity.IL2CPP 6.0.0.0", LoaderField(root, "F33_bep_il2cpp.dll", "loader_reference"));
        Assert.Equal("6.0.0.0", LoaderField(root, "F33_bep_il2cpp.dll", "loader_version"));
    }

    [Fact]
    public void Scan_DoesNotFlagUniformGenerationFolder()
    {
        // Both plugins are BepInEx 5 (Mono): no loader mismatch.
        _temp.CopyAll("F26_bep_meta.dll", "F28_bep_need.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex", "--json");

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            "bep_loader_mismatch",
            root.GetProperty("issues").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
    }

    [Fact]
    public void Scan_FlagsUniformPluginsAgainstDeclaredIl2CppHost()
    {
        // The literal "0 plugins to load" case: a folder of BepInEx 5 (Mono)
        // plugins with no loader DLL present, declared against an IL2CPP host.
        _temp.CopyAll("F26_bep_meta.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "unity-bepinex6-il2cpp", "--json");

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "bep_loader_mismatch");

        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Contains("IL2CPP", issue.GetProperty("summary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("blocked_loader_mismatch", BepState(root, "F26_bep_meta.dll"));
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

    private static string? LoaderField(JsonElement root, string fileName, string property)
    {
        foreach (JsonElement result in root.GetProperty("results").EnumerateArray())
        {
            if (Path.GetFileName(result.GetProperty("path").GetString()) == fileName)
                return result.GetProperty("bepinex").GetProperty(property).GetString();
        }

        throw new InvalidOperationException($"Result for {fileName} was not found.");
    }

    public void Dispose() => _temp.Dispose();
}
