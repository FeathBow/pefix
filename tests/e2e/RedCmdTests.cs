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
        RefPe.WriteVersionRef(path, "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = FileAssert.ReadBytes(path);

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0");

        Assert.Equal(0, result.ExitCode);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void RedirVerb_ApplyWritesAssemblyRefVersion()
    {
        string path = Path.Combine(_temp.DirPath, "apply.dll");
        RefPe.WriteVersionRef(path, "Newtonsoft.Json", new Version(9, 0, 0, 0));

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0",
            "--apply");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new Version(13, 0, 0, 0), ReadAssemblyRefVersion(path, "Newtonsoft.Json"));
    }

    [Fact]
    public void RedRefuse()
    {
        RefPe.WriteVersionRef(Path.Combine(_temp.DirPath, "a.dll"), "Newtonsoft.Json", new Version(9, 0, 0, 0));
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

        var root = JsonAssert.ParseObject(result.Stdout);
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    private static Version ReadAssemblyRefVersion(string path, string refName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (var handle in reader.AssemblyReferences)
            {
                var assemblyRef = reader.GetAssemblyReference(handle);
                if (string.Equals(reader.GetString(assemblyRef.Name), refName, StringComparison.Ordinal))
                    return assemblyRef.Version;
            }

            throw new InvalidOperationException($"AssemblyRef '{refName}' not found in {path}.");
        });
    }
}
