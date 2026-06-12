using System.IO;
using System.Linq;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class NativeGapTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_MissingNativeIsAdvisoryOnly()
    {
        // F04 P/Invokes a custom module named "native" that is not shipped.
        _temp.CopyAll("F04_x64_pinvoke.dll");

        CliResult result = CliRunner.Run(
            "scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--fail-on-issue");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "missing_native");
        Assert.Equal("native", issue.GetProperty("subject").GetString());
        Assert.Contains("not present in the scanned directory", issue.GetProperty("summary").GetString());
        Assert.DoesNotContain(
            "missing_native",
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
    }

    [Fact]
    public void Scan_PresentNativeWithMatchingMachineStaysSilent()
    {
        // F07 is a native AMD64 PE; renamed to the imported module name it
        // satisfies the x64-only consumer.
        _temp.CopyAll("F04_x64_pinvoke.dll", "F07_native_pe.dll");
        File.Move(Path.Combine(_temp.DirPath, "F07_native_pe.dll"), Path.Combine(_temp.DirPath, "native.dll"));

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "missing_native");
    }

    public void Dispose() => _temp.Dispose();
}
