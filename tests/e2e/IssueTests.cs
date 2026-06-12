using System.IO;
using System.Text.Json;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class IssueTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Scan_MissingRefs()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Missing references (2):", result.Stdout);
        Assert.Contains("Dependency: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.Contains("Microsoft.Extensions.DependencyInjection: F18_missing_refs.dll expects v1.0.0.0, but no provider was found", result.Stdout);
        Assert.Contains("Install the missing managed dependency into the scanned directory", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_MissingRefsJson()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement missing = root.GetProperty("missing_refs");
        JsonElement issues = root.GetProperty("issues");
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(2, missing.GetArrayLength());
        Assert.Equal(2, issues.GetArrayLength());
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal(2, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(["missing_ref"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal(2, summary.GetProperty("issues").GetInt32());
        Assert.Equal(2, summary.GetProperty("by_issue").GetProperty("missing_ref").GetInt32());
        Assert.All(issues.EnumerateArray(), issue => Assert.Equal("missing_ref", issue.GetProperty("code").GetString()));
        Assert.All(issues.EnumerateArray(), issue => Assert.False(issue.TryGetProperty("level", out _)));
        Assert.All(issues.EnumerateArray(), issue => Assert.Equal("assisted_fix", issue.GetProperty("repair_class").GetString()));
        Assert.All(issues.EnumerateArray(), issue => Assert.Contains("Install or restore the missing managed dependency", issue.GetProperty("repair_hint").GetString()));
        Assert.All(issues.EnumerateArray(), issue => Assert.Equal("pefix scan <path> --json", issue.GetProperty("verify_command").GetString()));
        Assert.All(issues.EnumerateArray(), issue => Assert.Contains("API compatibility", JsonAssert.StringArray(issue.GetProperty("unverified_risks"))[0]));
        JsonElement dependency = JsonAssert.SingleBy(missing, "assembly", "Dependency");
        Assert.Equal("F18_missing_refs.dll", dependency.GetProperty("required_by").GetString());
    }

    [Fact]
    public void Scan_DuplicateProvidersText()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Duplicate providers (1):", result.Stdout);
        Assert.Contains("CompatibleAnyCpu: PluginA.dll, PluginB.dll", result.Stdout);
        Assert.Contains("Remove or relocate duplicate provider copies", result.Stdout);
        Assert.DoesNotContain("All assemblies use compatible headers", result.Stdout);
    }

    [Fact]
    public void Scan_DuplicateProvidersJson()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("\r", result.Stdout);
        Assert.EndsWith("\n", result.Stdout);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement duplicateProviders = root.GetProperty("dup_providers");
        JsonElement issues = root.GetProperty("issues");
        JsonElement gate = root.GetProperty("gate");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(1, duplicateProviders.GetArrayLength());
        Assert.Equal(1, issues.GetArrayLength());
        Assert.Equal("dup_provider", issues[0].GetProperty("code").GetString());
        Assert.Equal("assisted_fix", issues[0].GetProperty("repair_class").GetString());
        Assert.Contains("Keep one provider copy", issues[0].GetProperty("repair_hint").GetString());
        Assert.Equal("pefix scan <path> --json", issues[0].GetProperty("verify_command").GetString());
        Assert.Contains("provider selection", JsonAssert.StringArray(issues[0].GetProperty("unverified_risks"))[0]);
        Assert.Equal(["dup_provider"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("pass", gate.GetProperty("version_conflict").GetString());
        Assert.Equal(1, summary.GetProperty("dup_providers").GetInt32());
        Assert.Equal(1, summary.GetProperty("issues").GetInt32());
        Assert.Equal("CompatibleAnyCpu", duplicateProviders[0].GetProperty("assembly").GetString());
        Assert.Equal(2, duplicateProviders[0].GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void Scan_DuplicateProvidersRelativePaths()
    {
        CopyDupDirs();
        var text = CliRunner.Run("scan", _temp.DirPath);
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, text.ExitCode);
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement duplicateProviders = doc.RootElement.GetProperty("dup_providers");
        JsonElement dupFiles = Assert.Single(duplicateProviders.EnumerateArray()).GetProperty("files");
        JsonElement issueFiles = Assert.Single(doc.RootElement.GetProperty("issues").EnumerateArray()).GetProperty("files");
        JsonElement summary = doc.RootElement.GetProperty("summary");
        const string fileA = "a/Plugin.dll";
        const string fileB = "b/Plugin.dll";

        Assert.Contains("2 require attention.", text.Stdout);
        Assert.Equal([fileA, fileB], JsonAssert.StringArray(dupFiles));
        Assert.Equal([fileA, fileB], JsonAssert.StringArray(issueFiles));
        Assert.Equal(1, summary.GetProperty("issues").GetInt32());
    }

    [Fact]
    public void Scan_DuplicateProvidersDoNotTriggerConflictGate()
    {
        CopyDup();
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_ConflictGatePassesWithoutConflict()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_ConflictGateFailsOnConflict()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Scan_ConflictGateJsonStillWritesReport()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json", "--fail-on-conflict");
        Assert.Equal(1, result.ExitCode);

        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement gate = root.GetProperty("gate");
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("fail", gate.GetProperty("version_conflict").GetString());
        Assert.Equal(["asm_conflict"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
    }

    [Fact]
    public void Scan_ConflictJson()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement root = doc.RootElement;
        JsonElement gate = root.GetProperty("gate");
        Assert.Equal("fail", gate.GetProperty("integrity").GetString());
        Assert.Equal("fail", gate.GetProperty("version_conflict").GetString());
        Assert.Equal(["asm_conflict"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal("asm_conflict", root.GetProperty("issues")[0].GetProperty("code").GetString());
        Assert.Equal("assisted_fix", root.GetProperty("issues")[0].GetProperty("repair_class").GetString());
        Assert.Contains("Align the directory", root.GetProperty("issues")[0].GetProperty("repair_hint").GetString());
        Assert.Equal("pefix scan <path> --json", root.GetProperty("issues")[0].GetProperty("verify_command").GetString());
        Assert.Contains("API compatibility", JsonAssert.StringArray(root.GetProperty("issues")[0].GetProperty("unverified_risks"))[0]);
    }

    [Fact]
    public void Scan_ConflictRelativePaths()
    {
        CopyConflict();
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement conflict = Assert.Single(doc.RootElement.GetProperty("conflicts").EnumerateArray());
        const string referencedBy = "references/Consumer.dll";
        const string providedBy = "providers/CompatibleAnyCpu.dll";

        Assert.Equal(referencedBy, conflict.GetProperty("referenced_by").GetString());
        Assert.Equal(providedBy, conflict.GetProperty("provided_by").GetString());
    }

    [Fact]
    public void Scan_MissingRefsDoNotTriggerConflictGate()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        var result = CliRunner.Run("scan", _temp.DirPath, "--fail-on-conflict");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Scan_MissingRefsRelativePaths()
    {
        CopyMissRefs();
        var result = CliRunner.Run("scan", _temp.DirPath, "--json");
        Assert.Equal(0, result.ExitCode);

        using JsonDocument doc = JsonDocument.Parse(result.Stdout);
        JsonElement missing = doc.RootElement.GetProperty("missing_refs");
        const string requiredBy = "references/F18_missing_refs.dll";

        Assert.Equal(2, missing.GetArrayLength());
        Assert.All(missing.EnumerateArray(), item => Assert.Equal(requiredBy, item.GetProperty("required_by").GetString()));
    }

    [Fact]
    public void Scan_SortsDistinctIssueCodes()
    {
        CopyDup();
        _temp.CopyAll("F17_conflict.dll", "F18_missing_refs.dll");

        var result = CliRunner.Run("scan", _temp.DirPath, "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement gate = root.GetProperty("gate");
        Assert.Equal(["asm_conflict", "dup_provider", "missing_ref"], JsonAssert.StringArray(gate.GetProperty("issue_codes")));
        Assert.Equal(4, gate.GetProperty("issue_count").GetInt32());
        Assert.Equal(2, root.GetProperty("summary").GetProperty("by_issue").GetProperty("missing_ref").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_issue").GetProperty("dup_provider").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_issue").GetProperty("asm_conflict").GetInt32());
    }

    [Fact]
    public void Scan_MissingMemberJson()
    {
        _temp.Copy("F36_member_consumer.dll");
        File.Copy(Paths.Get("F34_member_provider_thin.dll"), Path.Combine(_temp.DirPath, "MemberProvider.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "missing_member");
        JsonElement evidence = issue.GetProperty("evidence");

        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["missing_member"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("MemberProvider", issue.GetProperty("subject").GetString());
        Assert.Equal(["F36_member_consumer.dll", "MemberProvider.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal("MemberProvider.Api", evidence.GetProperty("type_name").GetString());
        Assert.Equal("Foo", evidence.GetProperty("member").GetString());
        Assert.Equal(2, evidence.GetProperty("parameter_count").GetInt32());
        Assert.Equal("name+parameter-count", evidence.GetProperty("matching_tier").GetString());
        Assert.Equal("MemberProvider.dll", evidence.GetProperty("provided_by").GetString());
    }

    [Fact]
    public void Scan_MissingMemberText()
    {
        _temp.Copy("F36_member_consumer.dll");
        File.Copy(Paths.Get("F34_member_provider_thin.dll"), Path.Combine(_temp.DirPath, "MemberProvider.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[missing_member] MemberProvider", result.Stdout);
        Assert.Contains("Method 'MemberProvider.Api.Foo' (2 args) not found in MemberProvider.dll", result.Stdout);
        Assert.Contains("consumed by F36_member_consumer.dll", result.Stdout);
    }

    [Fact]
    public void Scan_MissingTypeJson()
    {
        _temp.Copy("F36_member_consumer.dll");
        File.Copy(Paths.Get("F41_missing_type_provider.dll"), Path.Combine(_temp.DirPath, "MemberProvider.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        JsonElement missing = Assert.Single(root.GetProperty("missing_types").EnumerateArray());
        JsonElement issue = JsonAssert.SingleBy(root.GetProperty("issues"), "code", "missing_type");
        JsonElement evidence = issue.GetProperty("evidence");

        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal(["missing_type"], JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));
        Assert.Equal("MemberProvider", missing.GetProperty("assembly").GetString());
        Assert.Equal("MemberProvider.Api", missing.GetProperty("type").GetString());
        Assert.Equal("F36_member_consumer.dll", missing.GetProperty("referenced_by").GetString());
        Assert.Equal("MemberProvider.dll", missing.GetProperty("provided_by").GetString());
        Assert.Equal("MemberProvider", issue.GetProperty("subject").GetString());
        Assert.Equal(["F36_member_consumer.dll", "MemberProvider.dll"], JsonAssert.StringArray(issue.GetProperty("files")));
        Assert.Equal("MemberProvider.Api", evidence.GetProperty("type_name").GetString());
        Assert.Equal("MemberProvider.dll", evidence.GetProperty("provided_by").GetString());
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_member");
    }

    [Fact]
    public void Scan_MissingTypeText()
    {
        _temp.Copy("F36_member_consumer.dll");
        File.Copy(Paths.Get("F41_missing_type_provider.dll"), Path.Combine(_temp.DirPath, "MemberProvider.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Missing Types (1):", result.Stdout);
        Assert.Contains("MemberProvider: type MemberProvider.Api referenced by F36_member_consumer.dll", result.Stdout);
        Assert.Contains("[missing_type] MemberProvider", result.Stdout);
        Assert.Contains("Type 'MemberProvider.Api' not found in MemberProvider.dll", result.Stdout);
    }

    [Fact]
    public void Scan_ForwardedTypeDoesNotReportTypeOrMemberGap()
    {
        _temp.Copy("F45_forwarded_consumer.dll");
        File.Copy(Paths.Get("F43_forwarded_provider_fwd.dll"), Path.Combine(_temp.DirPath, "ForwardedProvider.dll"), overwrite: true);
        File.Copy(Paths.Get("F44_forwarded_target.dll"), Path.Combine(_temp.DirPath, "ForwardedTarget.dll"), overwrite: true);

        CliResult result = CliRunner.Run("scan", _temp.DirPath, "--profile", "publish-dir", "--json");

        Assert.Equal(0, result.ExitCode);
        JsonElement root = JsonAssert.ParseObject(result.Stdout);
        Assert.Empty(root.GetProperty("missing_types").EnumerateArray());
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_type");
        Assert.DoesNotContain(root.GetProperty("issues").EnumerateArray(), item => item.GetProperty("code").GetString() == "missing_member");
    }

    private void CopyDup()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginA.dll"), overwrite: true);
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginB.dll"), overwrite: true);
    }

    private void CopyDupDirs()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        string dirA = Path.Combine(_temp.DirPath, "a");
        string dirB = Path.Combine(_temp.DirPath, "b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        File.Copy(source, Path.Combine(dirA, "Plugin.dll"), overwrite: true);
        File.Copy(source, Path.Combine(dirB, "Plugin.dll"), overwrite: true);
    }

    private void CopyConflict()
    {
        string providerDir = Path.Combine(_temp.DirPath, "providers");
        string refDir = Path.Combine(_temp.DirPath, "references");
        Directory.CreateDirectory(providerDir);
        Directory.CreateDirectory(refDir);
        File.Copy(Paths.Get("F01_compatible_anycpu.dll"), Path.Combine(providerDir, "CompatibleAnyCpu.dll"), overwrite: true);
        File.Copy(Paths.Get("F17_conflict.dll"), Path.Combine(refDir, "Consumer.dll"), overwrite: true);
    }

    private void CopyMissRefs()
    {
        string refDir = Path.Combine(_temp.DirPath, "references");
        Directory.CreateDirectory(refDir);
        File.Copy(Paths.Get("F18_missing_refs.dll"), Path.Combine(refDir, "F18_missing_refs.dll"), overwrite: true);
    }

    public void Dispose() => _temp.Dispose();
}
