using PeFix.Cli;

namespace PeFix.Tests;

public sealed class DirectoryIssueBuilderTests
{
    [Fact]
    public void Build_ConflictIssue()
    {
        DirectoryIssue issue = SingleIssue(
            conflicts: [new DirectoryConflict("Dependency", "1.0.0.0", "2.0.0.0", "Plugin.dll", "Dependency.dll")]);

        AssertIssue(
            issue,
            IssueCode.AsmConflict,
            "Dependency",
            ["Plugin.dll", "Dependency.dll"],
            "Align the directory",
            "API compatibility");
        Assert.Contains("Plugin.dll expects v1.0.0.0", issue.Summary);
        Assert.Contains("v2.0.0.0 is provided by Dependency.dll", issue.Summary);
    }

    [Fact]
    public void Build_MissingRefIssue()
    {
        DirectoryIssue issue = SingleIssue(
            missingReferences: [new DirectoryMissingReference("Missing.Core", "3.0.0.0", "Plugin.dll")]);

        AssertIssue(
            issue,
            IssueCode.MissingRef,
            "Missing.Core",
            ["Plugin.dll"],
            "Install or restore the missing managed dependency",
            "API compatibility");
        Assert.Contains("Plugin.dll expects v3.0.0.0", issue.Summary);
        Assert.Contains("no provider was found", issue.Summary);
    }

    [Fact]
    public void Build_DuplicateProviderIssue()
    {
        DirectoryIssue issue = SingleIssue(
            duplicateProviders: [new DirectoryDuplicateProvider("Shared.Core", ["a/Shared.Core.dll", "b/Shared.Core.dll"])]);

        AssertIssue(
            issue,
            IssueCode.DupProvider,
            "Shared.Core",
            ["a/Shared.Core.dll", "b/Shared.Core.dll"],
            "Keep one provider copy",
            "provider selection");
        Assert.Contains("a/Shared.Core.dll, b/Shared.Core.dll", issue.Summary);
    }

    private static DirectoryIssue SingleIssue(
        DirectoryConflict[]? conflicts = null,
        DirectoryMissingReference[]? missingReferences = null,
        DirectoryDuplicateProvider[]? duplicateProviders = null)
    {
        DirectoryIssue[] issues = DirectoryIssueBuilder.Build(
            conflicts ?? [],
            missingReferences ?? [],
            duplicateProviders ?? []);
        return Assert.Single(issues);
    }

    private static void AssertIssue(
        DirectoryIssue issue,
        string code,
        string subject,
        string[] files,
        string hint,
        string risk)
    {
        Assert.Equal(code, issue.Code);
        Assert.Equal(subject, issue.Subject);
        Assert.Equal(files, issue.Files);
        Assert.Equal(RepairClass.AssistedFix, issue.RepairClass);
        Assert.Contains(hint, issue.RepairHint);
        Assert.Equal("pefix scan <path> --json", issue.VerifyCommand);
        Assert.Contains(risk, issue.UnverifiedRisks[0]);
    }
}
