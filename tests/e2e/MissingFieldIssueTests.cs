using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class MissingFieldIssueTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_MissingFieldJson()
    {
        CopyFieldGap();

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "publish-dir",
            "--json",
            "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "missing_field");
        JsonElement evidence = issue.GetProperty("evidence");

        Assert.False(root.TryGetProperty("missing_fields", out _));
        Assert.Empty(root.GetProperty("missing_types").EnumerateArray());
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["missing_field"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("FieldProvider", issue.GetProperty("subject").GetString());
        Assert.Equal(["F49_field_consumer.dll", "FieldProvider.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal("FieldProvider.Api", evidence.GetProperty("type_name").GetString());
        Assert.Equal("Value", evidence.GetProperty("member").GetString());
        Assert.Equal("name", evidence.GetProperty("matching_tier").GetString());
        Assert.Equal("FieldProvider.dll", evidence.GetProperty("provided_by").GetString());
        Assert.Equal(JsonValueKind.Null, evidence.GetProperty("parameter_count").ValueKind);
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_member");
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_type");
    }

    [Fact]
    public void Scan_MissingFieldText()
    {
        CopyFieldGap();

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[missing_field] FieldProvider", result.Stdout);
        Assert.Contains("references field FieldProvider.Api.Value", result.Stdout);
        Assert.Contains("tier name", result.Stdout);
        Assert.Contains("does not expose a matching field", result.Stdout);
        Assert.DoesNotContain("[missing_member]", result.Stdout);
        Assert.DoesNotContain("[missing_type]", result.Stdout);
    }

    [Fact]
    public void Scan_ForwardedTypeDoesNotReportFieldGap()
    {
        _temp.Copy("F45_forwarded_consumer.dll");
        File.Copy(Paths.Get("F43_forwarded_provider_fwd.dll"), Path.Combine(_temp.DirPath, "ForwardedProvider.dll"), overwrite: true);
        File.Copy(Paths.Get("F44_forwarded_target.dll"), Path.Combine(_temp.DirPath, "ForwardedTarget.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_field");
    }

    public void Dispose() => _temp.Dispose();

    private void CopyFieldGap()
    {
        _temp.Copy("F49_field_consumer.dll");
        File.Copy(Paths.Get("F47_field_provider_thin.dll"), Path.Combine(_temp.DirPath, "FieldProvider.dll"), overwrite: true);
    }
}
