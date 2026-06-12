using System.Linq;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class ImplGapIssueTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_MissingImplJson()
    {
        // The provider interface gained Reset() after the consumer was built:
        // every concrete implementing class now throws TypeLoadException at load.
        _temp.CopyAll("F52_impl_consumer.dll", "F51_impl_provider_new.dll");

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
            .Where(item => item.GetProperty("code").GetString() == "missing_impl")];

        Assert.Equal(3, issues.Length);
        Assert.Equal(["missing_impl"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        string[] implClasses = [.. issues
            .Select(item => item.GetProperty("evidence").GetProperty("impl_class").GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal)];
        Assert.Equal(["ImplConsumer.Derived", "ImplConsumer.Explicit", "ImplConsumer.Worker"], implClasses);

        JsonElement evidence = issues[0].GetProperty("evidence");
        Assert.Equal("ImplProvider.IWork", evidence.GetProperty("type_name").GetString());
        Assert.Equal("Reset", evidence.GetProperty("member").GetString());
        Assert.Equal(0, evidence.GetProperty("parameter_count").GetInt32());
        Assert.Equal("name+parameter-count", evidence.GetProperty("matching_tier").GetString());
        Assert.Equal("F51_impl_provider_new.dll", evidence.GetProperty("provided_by").GetString());
        Assert.DoesNotContain(
            issues,
            item => item.GetProperty("evidence").GetProperty("member").GetString() == "Log");
        Assert.DoesNotContain(
            issues,
            item => item.GetProperty("evidence").GetProperty("impl_class").GetString() == "ImplConsumer.Partial");
    }

    [Fact]
    public void Scan_MissingImplText()
    {
        _temp.CopyAll("F52_impl_consumer.dll", "F51_impl_provider_new.dll");

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[missing_impl] ImplProvider", result.Stdout);
        Assert.Contains("Class 'ImplConsumer.Worker' in F52_impl_consumer.dll does not implement 'ImplProvider.IWork.Reset'", result.Stdout);
    }

    [Fact]
    public void Scan_MatchingProviderReportsNoImplGap()
    {
        // Control: explicit implementation, base-chain implementation, and an
        // abstract implementer against the matching provider must stay silent.
        _temp.CopyAll("F52_impl_consumer.dll", "F50_impl_provider_old.dll");

        CliResult result = CliRunner.Run(
            "scan",
            _temp.DirPath,
            "--profile",
            "publish-dir",
            "--json",
            "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Empty(root.GetProperty("issues").EnumerateArray());
    }

    public void Dispose() => _temp.Dispose();
}
