using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ReflectionMissingScanTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_PublishDirReportsOnlyMissingLiteralReflectionLoad()
    {
        _temp.CopyAll(
            "F37_reflection_target.dll",
            "F38_reflection_present.dll",
            "F39_reflection_missing.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = ReflectionIssue(root);
        Assert.Equal("ReflectionMissingDependency", issue.GetProperty("subject").GetString());
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), IsReflectionTargetIssue);
        Assert.Equal(1, root.GetProperty("gate").GetProperty("issue_count").GetInt32());
    }

    [Fact]
    public void Scan_CustomResolverDowngradesReflectionMissingToAdvisory()
    {
        _temp.CopyAll("F40_reflection_resolver.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("reflection_missing", ReflectionIssue(root).GetProperty("code").GetString());
        AssertAdvisoryGate(root);
    }

    [Fact]
    public void Scan_NonPublishDirReflectionMissingIsAdvisory()
    {
        _temp.CopyAll("F39_reflection_missing.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "dotnet-plugin", "--json", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("ReflectionMissingDependency", ReflectionIssue(root).GetProperty("subject").GetString());
        AssertAdvisoryGate(root);
    }

    public void Dispose() => _temp.Dispose();

    private static JsonElement ReflectionIssue(JsonElement root)
    {
        return JsonAssert.SingleBy(root.GetProperty("issues"), "code", "reflection_missing");
    }

    private static bool IsReflectionTargetIssue(JsonElement issue)
    {
        return issue.GetProperty("code").GetString() == "reflection_missing"
            && issue.GetProperty("subject").GetString() == "ReflectionTarget";
    }

    private static void AssertAdvisoryGate(JsonElement root)
    {
        JsonElement gate = root.GetProperty("gate");
        Assert.Equal("pass", gate.GetProperty("integrity").GetString());
        Assert.Equal(0, gate.GetProperty("issue_count").GetInt32());
        Assert.Empty(JsonAssert.StringArray(gate.GetProperty("issue_codes")));
    }
}
