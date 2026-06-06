using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

public sealed class DirectoryIssueBuilderTests
{
    [Fact]
    public void Build_ConflictIssue()
    {
        DirectoryIssue issue = SingleIssue(new RefFinding(
            Tier: RefTier.AssemblyRef,
            Resolution: RefOutcome.VersionConflict,
            Confidence: Confidence.Gate,
            ConsumerPath: Abs("Plugin.dll"),
            ReferenceName: "Dependency",
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: "1.0.0.0",
            ActualVersion: "2.0.0.0",
            ProviderPath: Abs("Dependency.dll"),
            ProviderPaths: null));

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
        DirectoryIssue issue = SingleIssue(new RefFinding(
            Tier: RefTier.AssemblyRef,
            Resolution: RefOutcome.Missing,
            Confidence: Confidence.Gate,
            ConsumerPath: Abs("Plugin.dll"),
            ReferenceName: "Missing.Core",
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            "3.0.0.0",
            ActualVersion: null,
            ProviderPath: null,
            ProviderPaths: null));

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
        DirectoryIssue issue = SingleIssue(new RefFinding(
            Tier: RefTier.Provider,
            Resolution: RefOutcome.DuplicateProvider,
            Confidence: Confidence.Gate,
            ConsumerPath: string.Empty,
            ReferenceName: "Shared.Core",
            TypeName: null,
            MemberName: null,
            ParameterCount: null,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: null,
            ProviderPaths: [Abs("a/Shared.Core.dll"), Abs("b/Shared.Core.dll")]));

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
        DirectoryIssue issue = SingleIssue(new RefFinding(
            Tier: RefTier.MemSurface,
            Resolution: RefOutcome.MemberGap,
            Confidence: Confidence.Gate,
            ConsumerPath: Abs("Plugin.dll"),
            ReferenceName: "Shared.Core",
            TypeName: "Shared.Api",
            MemberName: "Foo",
            ParameterCount: 2,
            ExpectedVersion: null,
            ActualVersion: null,
            ProviderPath: Abs("Shared.Core.dll"),
            ProviderPaths: null));

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

    private static DirectoryIssue SingleIssue(RefFinding finding)
    {
        DirectoryIssue[] issues = DirectoryIssueBuilder.Build([finding], Rel());
        return Assert.Single(issues);
    }

    private static PathRelativizer Rel() => new(Root);

    private static string Abs(string path) => Path.Combine(Root, path);

    private static string Root => Path.Combine(Path.GetTempPath(), "pefix-tests");

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
