namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class RedCmdTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void RedirDryRunsWithoutApplyFlag()
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
    public void RedirJsonDryRun()
    {
        string path = Path.Combine(_temp.DirPath, "json-dry.dll");
        RefPe.WriteVersionRef(path, "Newtonsoft.Json", new Version(9, 0, 0, 0));
        byte[] before = FileAssert.ReadBytes(path);

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0",
            "--json");

        Assert.Equal(0, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.True(root.GetProperty("dry_run").GetBoolean());
        Assert.Equal("guided_fix", root.GetProperty("repair_class").GetString());
        var target = Assert.Single(root.GetProperty("targets").EnumerateArray());
        Assert.Equal("AssemblyRef", target.GetProperty("table").GetString());
        Assert.Equal(1, target.GetProperty("row").GetInt32());
        string[] risks = JsonAssert.StringArray(root.GetProperty("unverified_risks"));
        Assert.Contains("API/ABI compatibility", risks[0]);
        Assert.Contains("runtime load success", risks[0]);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void RedirDryRunBlock()
    {
        string path = Path.Combine(_temp.DirPath, "text-dry.dll");
        RefPe.WriteVersionRef(path, "Newtonsoft.Json", new Version(9, 0, 0, 0));

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Details:", result.Stdout);
        Assert.Contains("Targets:", result.Stdout);
        Assert.Contains("AssemblyRef row 1", result.Stdout);
        Assert.Contains("Repair Class:", result.Stdout);
        Assert.Contains("guided_fix", result.Stdout);
        Assert.Contains("Not Proven:", result.Stdout);
        Assert.Contains("API/ABI compatibility", result.Stdout);
        Assert.Contains("runtime load success", result.Stdout);
    }

    [Fact]
    public void RedirApplyWritesAssemblyRefVersion()
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
        Assert.Contains("Run pefix scan <dir> --json", result.Stdout);
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
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    [Fact]
    public void RedRefuseFileJson()
    {
        string path = _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run(
            "redir",
            path,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0",
            "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.EndsWith("F07_native_pe.dll", root.GetProperty("path").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
    }

    [Fact]
    public void RedirDirPlanFailureIsIoError()
    {
        string candidate = Path.Combine(_temp.DirPath, "candidate.dll");
        string blocked = Path.Combine(_temp.DirPath, "blocked.dll");
        RefPe.WriteVersionRef(candidate, "Newtonsoft.Json", new Version(9, 0, 0, 0));
        RefPe.WriteVersionRef(blocked, "Newtonsoft.Json", new Version(9, 0, 0, 0));
        Directory.CreateDirectory(blocked + ".pefix-plan.json");

        CliResult result = CliRunner.Run(
            "redir",
            _temp.DirPath,
            "--from",
            "Newtonsoft.Json:9.0.0.0",
            "--to",
            "13.0.0.0",
            "--apply",
            "--no-backup",
            "--json");

        Assert.Equal(4, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains(".pefix-plan.json", result.Stderr);
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
