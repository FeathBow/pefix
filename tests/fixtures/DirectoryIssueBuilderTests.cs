using PeFix.Cli;
using PeFix.Meta;

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

    [Fact]
    public void Build_MissingMemberIssue()
    {
        string root = Path.Combine(Path.GetTempPath(), "pefix-tests");
        DirectoryIssue issue = SingleIssue(
            memberRefGaps:
            [
                new MemberRefGap(
                    "Shared.Core",
                    "Shared.Api",
                    "Foo",
                    2,
                    "name+parameter-count",
                    Path.Combine(root, "Plugin.dll"),
                    Path.Combine(root, "Shared.Core.dll"))
            ]);

        AssertIssue(
            issue,
            IssueCode.MissingMember,
            "Shared.Core",
            ["Plugin.dll", "Shared.Core.dll"],
            "Align the referencing assembly and provider assembly",
            "runtime load success");
        Assert.Contains("Shared.Api.Foo/2", issue.Summary);
        Assert.Contains("name+parameter-count", issue.Summary);
        Assert.Contains("does not expose a matching member", issue.Summary);
        Assert.Equal("Shared.Api", issue.Evidence?.TypeName);
        Assert.Equal("Foo", issue.Evidence?.MemberName);
        Assert.Equal(2, issue.Evidence?.ParameterCount);
        Assert.Equal("name+parameter-count", issue.Evidence?.MatchingTier);
        Assert.Equal("Shared.Core.dll", issue.Evidence?.ProviderFile);
    }

    private static DirectoryIssue SingleIssue(
        DirectoryConflict[]? conflicts = null,
        DirectoryMissingReference[]? missingReferences = null,
        DirectoryDuplicateProvider[]? duplicateProviders = null,
        MemberRefGap[]? memberRefGaps = null)
    {
        DirectoryIssue[] issues = DirectoryIssueBuilder.Build(new IssueSources
        {
            Conflicts = conflicts ?? [],
            MissingReferences = missingReferences ?? [],
            DuplicateProviders = duplicateProviders ?? [],
            MemberRefGaps = memberRefGaps ?? [],
            Rel = new PathRelativizer(Path.Combine(Path.GetTempPath(), "pefix-tests"))
        });
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
