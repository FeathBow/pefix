using System.Linq;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class AccessGapIssueTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_InaccessibleMemberJson()
    {
        // The consumer was compiled against a provider granting InternalsVisibleTo,
        // but the shipped provider does not: every internal reference now throws
        // MemberAccessException or TypeLoadException at runtime.
        _temp.CopyAll("F55_access_consumer.dll", "F54_access_provider_bare.dll");

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "publish-dir",
            "--json",
            "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement[] issues = [.. root.GetProperty("issues").EnumerateArray()
            .Where(item => item.GetProperty("code").GetString() == "inaccessible_member")];

        Assert.Equal(3, issues.Length);
        Assert.Contains(
            "inaccessible_member",
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));

        string[] summaries = [.. issues.Select(item => item.GetProperty("summary").GetString() ?? string.Empty)];
        Assert.Contains(summaries, item => item.Contains("Member 'AccessProvider.Api.Hidden'", StringComparison.Ordinal));
        Assert.Contains(summaries, item => item.Contains("Member 'AccessProvider.Api.Count'", StringComparison.Ordinal));
        Assert.Contains(summaries, item => item.Contains("Type 'AccessProvider.Inner'", StringComparison.Ordinal));
        Assert.DoesNotContain(summaries, item => item.Contains("Open", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_InternalsVisibleToSuppressesIssue()
    {
        _temp.CopyAll("F55_access_consumer.dll", "F53_access_provider_ivt.dll");

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "publish-dir",
            "--json",
            "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(JsonAssert.ParseObject(result.Stdout).GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public void Scan_IgnoresAccessChecksToSuppressesIssue()
    {
        _temp.CopyAll("F56_access_skipper.dll", "F54_access_provider_bare.dll");

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "publish-dir",
            "--json",
            "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "inaccessible_member");
    }

    [Fact]
    public void Scan_DotnetPluginProfileReportsAdvisoryOnly()
    {
        // Outside publish-dir the finding stays advisory: reported, not gating.
        // Relaxed Mono access checks and publicized references are common there.
        _temp.CopyAll("F55_access_consumer.dll", "F54_access_provider_bare.dll");

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "dotnet-plugin",
            "--json",
            "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Contains(
            root.GetProperty("issues").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "inaccessible_member");
        Assert.DoesNotContain(
            "inaccessible_member",
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    public void Dispose() => _temp.Dispose();
}
