using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ConflictProvidedLeafTests
{
    [Fact]
    public void FrameworkAssemblyVersionSkewIsNotAConflict()
    {
        // Real Unity games ship mscorlib v4 but reference v2 from older plugins;
        // the runtime unifies framework assemblies, so this is benign.
        Inspection[] all =
        [
            Asm("/s/mscorlib.dll", "mscorlib", "4.0.0.0", []),
            Asm("/s/Plugin.dll", "Plugin", "1.0.0.0", [new AssemblyIdentity("mscorlib", "2.0.0.0")]),
        ];

        DependencyIndex index = DependencyIndex.Build(all, HostProfile.UnityBepInEx);

        Assert.Empty(index.FindConflicts(all));
    }

    [Fact]
    public void NonProvidedAssemblyVersionSkewIsAConflict()
    {
        Inspection[] all =
        [
            Asm("/s/MyLib.dll", "MyLib", "2.0.0.0", []),
            Asm("/s/Plugin.dll", "Plugin", "1.0.0.0", [new AssemblyIdentity("MyLib", "1.0.0.0")]),
        ];

        DependencyIndex index = DependencyIndex.Build(all, HostProfile.UnityBepInEx);

        VersionConflict conflict = Assert.Single(index.FindConflicts(all));
        Assert.Equal("MyLib", conflict.AssemblyName);
    }

    private static Inspection Asm(string path, string name, string version, AssemblyIdentity[] references)
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
            AssemblyDefinition: new AssemblyIdentity(name, version),
            HasReadyToRun: null,
            IsTrimmable: null,
            BepInEx: null);
    }
}
