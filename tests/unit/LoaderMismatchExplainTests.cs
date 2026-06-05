using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class LoaderMismatchExplainTests
{
    private static readonly AssemblyIdentity[] Bep6Mono =
    [
        new("BepInEx.Core", "6.0.0.0"),
        new("BepInEx.Unity.Mono", "6.0.0.0"),
    ];

    private static readonly AssemblyIdentity[] Bep6Il2Cpp =
    [
        new("BepInEx.Core", "6.0.0.0"),
        new("BepInEx.Unity.IL2CPP", "6.0.0.0"),
        new("Il2CppInterop.Runtime", "1.0.0.0"),
    ];

    private static readonly AssemblyIdentity[] Bep5 =
    [
        new("BepInEx", "5.4.21.0"),
    ];

    [Fact]
    public void MixedMonoAndIl2CppPluginsAreFlagged()
    {
        Inspection[] results =
        [
            Plugin("/scan/MonoPlugin.dll", Bep6Mono, "mono.plugin"),
            Plugin("/scan/Il2CppPlugin.dll", Bep6Il2Cpp, "il2cpp.plugin"),
        ];

        BepInExExplainResult explain = Explain(results);

        DirectoryIssue issue = Assert.Single(
            [.. explain.Issues.Where(item => item.Code == IssueCode.BepLoaderMismatch)]);
        Assert.Equal(RepairClass.AssistedFix, issue.RepairClass);
        Assert.Equal(BepStateCode.LoaderMismatch, explain.StateForFile("/scan/MonoPlugin.dll"));
        Assert.Equal(BepStateCode.LoaderMismatch, explain.StateForFile("/scan/Il2CppPlugin.dll"));
        Assert.Contains("Mono", issue.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IL2CPP", issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MixedGeneration5And6AreFlagged()
    {
        Inspection[] results =
        [
            Plugin("/scan/Five.dll", Bep5, "five.plugin"),
            Plugin("/scan/Six.dll", Bep6Mono, "six.plugin"),
        ];

        Assert.Contains(Explain(results).Issues, item => item.Code == IssueCode.BepLoaderMismatch);
    }

    [Fact]
    public void UniformMonoPluginsAreNotFlagged()
    {
        Inspection[] results =
        [
            Plugin("/scan/A.dll", Bep6Mono, "a.plugin"),
            Plugin("/scan/B.dll", Bep6Mono, "b.plugin"),
        ];

        Assert.DoesNotContain(Explain(results).Issues, item => item.Code == IssueCode.BepLoaderMismatch);
        Assert.Equal(BepStateCode.Plugin, Explain(results).StateForFile("/scan/A.dll"));
    }

    [Fact]
    public void CoreOnlyPluginDoesNotConflictWithMono()
    {
        Inspection[] results =
        [
            Plugin("/scan/CoreOnly.dll", [new AssemblyIdentity("BepInEx.Core", "6.0.0.0")], "core.plugin"),
            Plugin("/scan/Mono.dll", Bep6Mono, "mono.plugin"),
        ];

        Assert.DoesNotContain(Explain(results).Issues, item => item.Code == IssueCode.BepLoaderMismatch);
    }

    [Fact]
    public void PluginsIncompatibleWithDetectedIl2CppHostAreFlagged()
    {
        Inspection[] results =
        [
            HostAssembly("/scan/core/BepInEx.Core.dll", "BepInEx.Core"),
            HostAssembly("/scan/core/BepInEx.Unity.IL2CPP.dll", "BepInEx.Unity.IL2CPP"),
            Plugin("/scan/plugins/MonoPlugin.dll", Bep6Mono, "mono.plugin"),
        ];

        BepInExExplainResult explain = Explain(results);

        DirectoryIssue issue = Assert.Single(
            [.. explain.Issues.Where(item => item.Code == IssueCode.BepLoaderMismatch)]);
        Assert.Contains("IL2CPP", issue.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BepStateCode.LoaderMismatch, explain.StateForFile("/scan/plugins/MonoPlugin.dll"));
    }

    [Fact]
    public void PluginsCompatibleWithDetectedHostAreNotFlagged()
    {
        Inspection[] results =
        [
            HostAssembly("/scan/core/BepInEx.Core.dll", "BepInEx.Core"),
            HostAssembly("/scan/core/BepInEx.Unity.Mono.dll", "BepInEx.Unity.Mono"),
            Plugin("/scan/plugins/MonoPlugin.dll", Bep6Mono, "mono.plugin"),
        ];

        Assert.DoesNotContain(Explain(results).Issues, item => item.Code == IssueCode.BepLoaderMismatch);
    }

    [Fact]
    public void DeclaredIl2CppHostFlagsUniformMonoPlugins()
    {
        Inspection[] results =
        [
            Plugin("/scan/MonoA.dll", Bep6Mono, "a.plugin"),
            Plugin("/scan/MonoB.dll", Bep6Mono, "b.plugin"),
        ];
        LoaderTarget declared = new(LoaderGeneration.BepInEx6, LoaderFlavor.Il2Cpp);

        BepInExExplainResult explain = BepInExExplain.Explain(
            results, new PathRelativizer("/scan"), BepInExProviderIndex.From(results), null, declared);

        Assert.Contains(explain.Issues, item => item.Code == IssueCode.BepLoaderMismatch);
        Assert.Equal(BepStateCode.LoaderMismatch, explain.StateForFile("/scan/MonoA.dll"));
    }

    private static BepInExExplainResult Explain(Inspection[] results)
    {
        return BepInExExplain.Explain(results, new PathRelativizer("/scan"), BepInExProviderIndex.From(results));
    }

    private static Inspection HostAssembly(string path, string definitionName)
    {
        return Plugin(path, [], string.Empty) with
        {
            AssemblyDefinition = new AssemblyIdentity(definitionName, "6.0.0.0"),
            BepInEx = null,
        };
    }

    private static Inspection Plugin(string path, AssemblyIdentity[] references, string guid)
    {
        return new Inspection(
            Path: path,
            ValidPe: true,
            HasCliHeader: true,
            PeFormat: "PE32",
            Machine: "I386",
            ManagedCorFlags: new ManagedCorFlags(true, false, false, false),
            Signals: new Signals(false, false, false, false),
            Category: Category.Portability,
            Status: Status.Compatible,
            ReasonCode: ReasonCode.Portable,
            PrimaryCause: string.Empty,
            RuntimeRisks: [],
            Warnings: [],
            NextSteps: [],
            LoadReqs: null,
            PInvokeDeps: null,
            Tfm: null,
            MetaVersion: "v4.0.30319",
            OsPlatforms: null,
            AssemblyReferences: references,
            AssemblyDefinition: new AssemblyIdentity(System.IO.Path.GetFileNameWithoutExtension(path), "1.0.0.0"),
            HasReadyToRun: null,
            IsTrimmable: null,
            BepInEx: new BepInExMetadata([new BepInExPluginMetadata(guid, guid, "1.0.0", [])]));
    }
}
