using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

public sealed class BepInExExplainTests
{
    [Fact]
    public void Explain_MissingHardDependency()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", [new BepInExDependencyMetadata("need.hard", ">=1.0.0", true)]);
        BepInExExplainResult result = BepInExExplain.Explain([plugin], Rel(), BepInExProviderIndex.From([plugin]));

        DirectoryIssue issue = Assert.Single(result.Issues);
        AssertIssue(issue, IssueCode.BepMissing, "need.hard", ["Plugin.dll"]);
        Assert.Contains("test.plugin requires BepInEx plugin need.hard", issue.Summary);
        Assert.Contains("no matching plugin GUID was found", issue.Summary);
    }

    [Fact]
    public void Explain_CaseMismatchHardDependency()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", [new BepInExDependencyMetadata("need.hard", null, true)]);
        Inspection provider = Plugin("Provider.dll", "NEED.HARD", []);
        BepInExExplainResult result = BepInExExplain.Explain([plugin, provider], Rel(), BepInExProviderIndex.From([plugin, provider]));

        DirectoryIssue issue = Assert.Single(result.Issues);
        AssertIssue(issue, IssueCode.BepCasing, "need.hard", ["Plugin.dll"]);
        Assert.Contains("only a case-different plugin GUID was found", issue.Summary);
    }

    [Fact]
    public void Explain_IgnoresPresentAndSoftDependencies()
    {
        Inspection plugin = Plugin(
            "Plugin.dll",
            "test.plugin",
            [
                new BepInExDependencyMetadata("need.hard", null, true),
                new BepInExDependencyMetadata("need.soft", null, false)
            ]);
        Inspection provider = Plugin("Provider.dll", "need.hard", []);
        BepInExExplainResult result = BepInExExplain.Explain([plugin, provider], Rel(), BepInExProviderIndex.From([plugin, provider]));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Explain_AssignsPluginAndHelperStatesInBepInExContext()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", []);
        Inspection helper = Managed("Helper.dll");
        BepInExExplainResult result = BepInExExplain.Explain([plugin, helper], Rel(), BepInExProviderIndex.From([plugin, helper]));

        Assert.Equal(BepInExExplainState.Plugin, result.StateForFile(Abs("Plugin.dll")));
        Assert.Equal(BepInExExplainState.HelperLibrary, result.StateForFile(Abs("Helper.dll")));
    }

    [Fact]
    public void Explain_AssignsInvalidArtifactStateInBepInExContext()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", []);
        Inspection invalid = Invalid("Reference.dll");
        BepInExExplainResult result = BepInExExplain.Explain([plugin, invalid], Rel(), BepInExProviderIndex.From([plugin, invalid]));

        Assert.Equal(BepInExExplainState.InvalidArtifact, result.StateForFile(Abs("Reference.dll")));
    }

    [Fact]
    public void Explain_DoesNotAssignHelperStateWithoutBepInExContext()
    {
        Inspection helper = Managed("Helper.dll");
        BepInExExplainResult result = BepInExExplain.Explain([helper], Rel(), BepInExProviderIndex.From([helper]));

        Assert.Null(result.StateForFile(Abs("Helper.dll")));
    }

    [Fact]
    public void Explain_AssignsBlockedStates()
    {
        Inspection missing = Plugin("Missing.dll", "test.missing", [new BepInExDependencyMetadata("need.hard", null, true)]);
        Inspection casing = Plugin("Casing.dll", "test.casing", [new BepInExDependencyMetadata("need.case", null, true)]);
        Inspection provider = Plugin("Provider.dll", "NEED.CASE", []);
        BepInExExplainResult result = BepInExExplain.Explain(
            [missing, casing, provider],
            Rel(),
            BepInExProviderIndex.From([missing, casing, provider]));

        Assert.Equal(BepInExExplainState.BlockedMissingDependency, result.StateForFile(Abs("Missing.dll")));
        Assert.Equal(BepInExExplainState.BlockedGuidCaseMismatch, result.StateForFile(Abs("Casing.dll")));
    }

    [Fact]
    public void Explain_DetectsDuplicateGuid()
    {
        Inspection first = Plugin("First.dll", "duplicateProvider.guid", []);
        Inspection second = Plugin("Second.dll", "duplicateProvider.guid", []);
        BepInExExplainResult result = BepInExExplain.Explain([first, second], Rel(), BepInExProviderIndex.From([first, second]));

        DirectoryIssue issue = Assert.Single(result.Issues.Where(item => item.Code == IssueCode.BepDuplicateGuid).ToArray());
        AssertIssue(
            issue,
            IssueCode.BepDuplicateGuid,
            "duplicateProvider.guid",
            ["First.dll", "Second.dll"],
            "chainloader selection");
        Assert.Contains("Multiple BepInEx plugins declare GUID duplicateProvider.guid", issue.Summary);
    }

    [Fact]
    public void Explain_DetectsVersionMismatch()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", [new BepInExDependencyMetadata("need.version", ">=2.0.0", true)]);
        Inspection provider = Plugin("Provider.dll", "need.version", [], version: "1.5.0");
        BepInExExplainResult result = BepInExExplain.Explain([plugin, provider], Rel(), BepInExProviderIndex.From([plugin, provider]));

        DirectoryIssue issue = Assert.Single(result.Issues.Where(item => item.Code == IssueCode.BepVersionMismatch).ToArray());
        AssertIssue(issue, IssueCode.BepVersionMismatch, "need.version", ["Plugin.dll", "Provider.dll"]);
        Assert.Equal(BepInExExplainState.BlockedVersionMismatch, result.StateForFile(Abs("Plugin.dll")));
        Assert.Contains("requires BepInEx plugin need.version >=2.0.0", issue.Summary);
        Assert.Contains("1.5.0 is provided by Provider.dll", issue.Summary);
    }

    [Fact]
    public void Explain_DetectsPluginUnresolvedChain()
    {
        Inspection plugin = Plugin("Plugin.dll", "test.plugin", [], assembly: "Plugin", references: [Identity("Helper", "1.0.0.0")]);
        Inspection helper = Managed("Helper.dll", assembly: "Helper", references: [Identity("Missing", "1.0.0.0")]);
        Inspection[] inspections = [plugin, helper];
        ClosureReport closure = ClosureGraph.Build(inspections, Root);

        BepInExExplainResult result = BepInExExplain.Explain(inspections, Rel(), BepInExProviderIndex.From(inspections), closure);

        DirectoryIssue issue = Assert.Single(result.Issues.Where(item => item.Code == IssueCode.PluginUnresolvedChain).ToArray());
        AssertIssue(issue, IssueCode.PluginUnresolvedChain, "Missing", ["Plugin.dll"], "runtime chainloader success");
        Assert.Equal(BepInExExplainState.RiskUnresolvedAssemblyChain, result.StateForFile(Abs("Plugin.dll")));
        Assert.Contains("Plugin.dll loads Helper.dll", issue.Summary);
        Assert.Contains("Helper.dll needs Missing.dll", issue.Summary);
    }

    private static void AssertIssue(DirectoryIssue issue, string code, string subject, string[] files)
    {
        AssertIssue(issue, code, subject, files, "chainloader success");
    }

    private static void AssertIssue(
        DirectoryIssue issue,
        string code,
        string subject,
        string[] files,
        string risk)
    {
        Assert.Equal(code, issue.Code);
        Assert.Equal(subject, issue.Subject);
        Assert.Equal(files, issue.Files);
        Assert.Equal(RepairClass.AssistedFix, issue.RepairClass);
        Assert.Equal("pefix scan <path> --json", issue.VerifyCommand);
        Assert.Contains(risk, issue.UnverifiedRisks[0]);
    }

    private static ScanPathRelativizer Rel()
    {
        return new ScanPathRelativizer(Root);
    }

    private static Inspection Plugin(
        string path,
        string guid,
        BepInExDependencyMetadata[] dependencies,
        string version = "1.0.0",
        string? assembly = null,
        AssemblyIdentity[]? references = null)
    {
        return Managed(
            path,
            assembly: assembly,
            references: references,
            BepInEx: new BepInExMetadata([new BepInExPluginMetadata(guid, "Plugin", version, dependencies)]));
    }

    private static Inspection Managed(
        string path,
        string? assembly = null,
        AssemblyIdentity[]? references = null,
        BepInExMetadata? BepInEx = null)
    {
        return new Inspection(
            Abs(path),
            true,
            true,
            "PE32",
            "I386",
            default,
            default,
            Category.Portability,
            Status.Compatible,
            ReasonCode.Portable,
            "cause",
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            references,
            assembly is null ? null : Identity(assembly, "1.0.0.0"),
            BepInEx: BepInEx);
    }

    private static Inspection Invalid(string path)
    {
        return new Inspection(
            Abs(path),
            true,
            true,
            "PE32",
            "I386",
            default,
            default,
            Category.RefAssembly,
            Status.Unsafe,
            ReasonCode.RefAssembly,
            "cause",
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            Identity("Reference", "1.0.0.0"));
    }

    private static string Abs(string path)
    {
        return Path.Combine(Root, path);
    }

    private static AssemblyIdentity Identity(string name, string version)
    {
        return new AssemblyIdentity(name, version);
    }

    private const string Root = "/scan-root";
}
