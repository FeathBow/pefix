using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class SnCmdTests : IDisposable
{
    private static readonly byte[] StrongNameToken = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11];
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SnRefuse()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");
        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--json");
        Assert.Equal(1, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        var refusal = Assert.Single(root.GetProperty("refusals").EnumerateArray());
        Assert.EndsWith("F07_native_pe.dll", refusal.GetProperty("path").GetString());
    }

    [Fact]
    public void SnRefuseFileJson()
    {
        string path = _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("snstrip", path, "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.EndsWith("F07_native_pe.dll", root.GetProperty("path").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
    }

    [Fact]
    public void SnStripJsonDryRun()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);
        Directory.CreateDirectory(sibling + ".pefix-plan.json");

        CliResult result = CliRunner.Run("snstrip", target, "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("dry_run", root.GetProperty("outcome").GetString());
        Assert.True(root.GetProperty("dry_run").GetBoolean());
        var rowTarget = Assert.Single(root.GetProperty("targets").EnumerateArray(), item =>
            item.GetProperty("kind").GetString() == "corflags");
        Assert.True(rowTarget.GetProperty("offset").GetInt64() > 0);
        Assert.Equal("guided_fix", root.GetProperty("repair_class").GetString());
        string[] risks = JsonAssert.StringArray(root.GetProperty("unverified_risks"));
        Assert.Contains("Assembly identity", risks[0]);
        Assert.Contains("signing/IVT compatibility", risks[0]);
        Assert.Contains("runtime load success", risks[0]);
        Assert.Empty(root.GetProperty("dep_fails").EnumerateArray());
        Assert.Single(root.GetProperty("deps").EnumerateArray());
    }

    [Fact]
    public void SnStripJsonDeps()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        CliResult result = CliRunner.Run("snstrip", target, "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        var dep = Assert.Single(root.GetProperty("deps").EnumerateArray());
        var depTarget = Assert.Single(dep.GetProperty("targets").EnumerateArray());
        Assert.Equal("AssemblyRef", depTarget.GetProperty("table").GetString());
        Assert.Equal(1, depTarget.GetProperty("row").GetInt32());
    }

    [Fact]
    public void SnStripDryRunsWithoutApplyFlag()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        byte[] before = FileAssert.ReadBytes(path);
        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        FileAssert.Unchanged(before, path);
    }

    [Fact]
    public void SnStripDryRunBlock()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        CliResult result = CliRunner.Run("snstrip", path);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status:  DRY-RUN", result.Stdout);
        Assert.Contains("Action:  Run:", result.Stdout);
        Assert.Contains("Details:", result.Stdout);
        Assert.Contains("Targets:", result.Stdout);
        Assert.Contains("corflags", result.Stdout);
        Assert.Contains("Dependency Targets:", result.Stdout);
        Assert.Contains("AssemblyRef row 1", result.Stdout);
        Assert.Contains("Repair Class:", result.Stdout);
        Assert.Contains("guided_fix", result.Stdout);
        Assert.Contains("Not Proven:", result.Stdout);
        Assert.Contains("Assembly identity", result.Stdout);
    }

    [Fact]
    public void SnStripApplyJsonContract()
    {
        string path = _temp.Copy("F03_x64_strongname.dll");
        CliResult result = CliRunner.Run("snstrip", path, "--apply", "--no-backup", "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("patched", root.GetProperty("outcome").GetString());
        Assert.True(root.GetProperty("was_patched").GetBoolean());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        Assert.Equal("guided_fix", root.GetProperty("repair_class").GetString());
        Assert.NotEmpty(root.GetProperty("unverified_risks").EnumerateArray());
        Assert.False(root.TryGetProperty("verify", out _));
        Assert.False(ReadCorFlags(path).HasFlag(CorFlags.StrongNameSigned));
        Assert.All(ReadAssemblyPublicKey(path), b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void SnStripDirJsonRewritesToken()
    {
        _temp.Copy("F03_x64_strongname.dll");
        string sibling = Path.Combine(_temp.DirPath, "sibling.dll");
        RefPe.WriteTokenRef(sibling, "X64StrongName", StrongNameToken);

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--no-backup", "--json");
        Assert.Equal(0, result.ExitCode);

        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("patched", root.GetProperty("outcome").GetString());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        Assert.Equal(1, root.GetProperty("deps_patched").GetInt32());
        Assert.Single(root.GetProperty("deps").EnumerateArray());
        var item = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal(1, item.GetProperty("schema_version").GetInt32());
        Assert.False(item.TryGetProperty("deps_patched", out _));
        Assert.False(item.TryGetProperty("deps", out _));
        Assert.False(item.TryGetProperty("dep_fails", out _));
        Assert.Equal("guided_fix", item.GetProperty("repair_class").GetString());
        Assert.NotEmpty(item.GetProperty("unverified_risks").EnumerateArray());
        byte[] token = ReadAssemblyRefPublicKeyToken(sibling, "X64StrongName");
        Assert.NotEmpty(token);
        Assert.All(token, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void SnStripDirJsonRefusedOutcome()
    {
        _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--json");

        Assert.Equal(1, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("refused", root.GetProperty("outcome").GetString());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        Assert.Empty(root.GetProperty("results").EnumerateArray());
        Assert.Single(root.GetProperty("refusals").EnumerateArray());
    }

    [Fact]
    public void SnStripDirJsonKeepsGoodResultsWhenRefused()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--json");

        Assert.Equal(1, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("dry_run", root.GetProperty("outcome").GetString());
        Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Single(root.GetProperty("refusals").EnumerateArray());
    }

    [Fact]
    public void SnStripDirApplyRefusalKeepsPlanResults()
    {
        _temp.Copy("F03_x64_strongname.dll");
        _temp.Copy("F07_native_pe.dll");

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--no-backup", "--json");

        Assert.Equal(1, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("refused", root.GetProperty("outcome").GetString());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        var item = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal("dry_run", item.GetProperty("outcome").GetString());
        Assert.Single(root.GetProperty("refusals").EnumerateArray());
    }

    [Fact]
    public void SnStripDirJsonUnchangedOutcome()
    {
        _temp.Copy("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--json");

        Assert.Equal(0, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("unchanged", root.GetProperty("outcome").GetString());
        Assert.False(root.GetProperty("dry_run").GetBoolean());
        Assert.Empty(root.GetProperty("results").EnumerateArray());
        Assert.Empty(root.GetProperty("deps").EnumerateArray());
    }

    [Fact]
    public void SnStripFileJsonUnsignedIsDiagnostic()
    {
        string path = _temp.Copy("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("snstrip", path, "--apply", "--json");

        Assert.Equal(0, result.ExitCode);
        var root = JsonAssert.ParseObject(result.Stdout);
        Assert.Equal("unsigned", root.GetProperty("outcome").GetString());
        Assert.Equal("diagnostic_only", root.GetProperty("repair_class").GetString());
    }

    [Fact]
    public void SnStripFileTextUnsignedIsDiagnostic()
    {
        string path = _temp.Copy("F01_compatible_anycpu.dll");

        CliResult result = CliRunner.Run("snstrip", path, "--apply");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Repair Class:", result.Stdout);
        Assert.Contains("diagnostic_only", result.Stdout);
        Assert.DoesNotContain("guided_fix", result.Stdout);
    }

    [Fact]
    public void SnStripDirPlanFailureIsIoError()
    {
        string target = _temp.Copy("F03_x64_strongname.dll");
        string blocked = Path.Combine(_temp.DirPath, "blocked.dll");
        File.Copy(target, blocked);
        Directory.CreateDirectory(blocked + ".pefix-plan.json");

        CliResult result = CliRunner.Run("snstrip", _temp.DirPath, "--apply", "--no-backup", "--json");

        Assert.Equal(4, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains(".pefix-plan.json", result.Stderr);
    }

    private static CorFlags ReadCorFlags(string path)
    {
        return PeRead.Pe(path, pe => pe.PEHeaders.CorHeader!.Flags);
    }

    private static byte[] ReadAssemblyPublicKey(string path)
    {
        return PeRead.Meta(path, reader =>
            reader.GetBlobBytes(reader.GetAssemblyDefinition().PublicKey));
    }

    private static byte[] ReadAssemblyRefPublicKeyToken(string path, string name)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (AssemblyReferenceHandle h in reader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = reader.GetAssemblyReference(h);
                if (reader.GetString(assemblyRef.Name) == name)
                    return reader.GetBlobBytes(assemblyRef.PublicKeyOrToken);
            }
            throw new InvalidOperationException($"AssemblyRef '{name}' was not found in {path}.");
        });
    }
}
