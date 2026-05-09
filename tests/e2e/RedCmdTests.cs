namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class RedCmdTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void RedirVerb_NoApplyFlag_DryRunsOnly()
    {
        string path = Path.Combine(_temp.DirPath, "dry.dll");
        RefPe.WriteVer(path, "Newtonsoft.Json", new Version(9, 0, 0, 0));
        DateTime before = File.GetLastWriteTimeUtc(path);

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void RedRefuse()
    {
        RefPe.WriteVer(Path.Combine(_temp.DirPath, "a.dll"), "Newtonsoft.Json", new Version(9, 0, 0, 0));
        _temp.Copy("F07_native_pe.dll");
        CliResult result = CliRunner.Run(
            "redir",
            _temp.DirPath,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0",
            "--json");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"refusals\"", result.Stdout);
        Assert.Contains("F07_native_pe.dll", result.Stdout);
    }
}
