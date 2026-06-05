using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class LoaderTargetTests
{
    [Fact]
    public void BepInEx5MonolithicReferenceIsBep5Mono()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx", "5.4.21.0"),
            new AssemblyIdentity("UnityEngine", "0.0.0.0"),
        ]);

        Assert.Equal(LoaderGeneration.BepInEx5, target.Generation);
        Assert.Equal(LoaderFlavor.Mono, target.Flavor);
        Assert.True(target.IsBepInExTarget);
    }

    [Fact]
    public void BepInEx6UnityMonoReferenceIsBep6Mono()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx.Core", "6.0.0.0"),
            new AssemblyIdentity("BepInEx.Unity.Mono", "6.0.0.0"),
        ]);

        Assert.Equal(LoaderGeneration.BepInEx6, target.Generation);
        Assert.Equal(LoaderFlavor.Mono, target.Flavor);
    }

    [Fact]
    public void BepInEx6Il2CppReferenceIsBep6Il2Cpp()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx.Core", "6.0.0.0"),
            new AssemblyIdentity("BepInEx.Unity.IL2CPP", "6.0.0.0"),
            new AssemblyIdentity("Il2CppInterop.Runtime", "1.0.0.0"),
        ]);

        Assert.Equal(LoaderGeneration.BepInEx6, target.Generation);
        Assert.Equal(LoaderFlavor.Il2Cpp, target.Flavor);
    }

    [Fact]
    public void BepInEx6CoreOnlyReferenceIsBep6UnknownFlavor()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx.Core", "6.0.0.0"),
        ]);

        Assert.Equal(LoaderGeneration.BepInEx6, target.Generation);
        Assert.Equal(LoaderFlavor.Unknown, target.Flavor);
    }

    [Fact]
    public void ReferenceCapturesLoaderBuildPreferringCore()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx.Core", "6.0.0.0"),
            new AssemblyIdentity("BepInEx.Unity.IL2CPP", "6.0.0.0"),
        ]);

        Assert.Equal("BepInEx.Core 6.0.0.0", target.Reference);
    }

    [Fact]
    public void ReferenceFallsBackToFlavorAssemblyWhenNoCore()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("BepInEx.Unity.IL2CPP", "6.0.0.0"),
        ]);

        Assert.Equal("BepInEx.Unity.IL2CPP 6.0.0.0", target.Reference);
    }

    [Fact]
    public void NonBepInExReferencesAreUnknownTarget()
    {
        LoaderTarget target = LoaderTargetReader.FromReferences(
        [
            new AssemblyIdentity("System.Runtime", "8.0.0.0"),
            new AssemblyIdentity("Newtonsoft.Json", "13.0.0.0"),
        ]);

        Assert.False(target.IsBepInExTarget);
        Assert.Equal(LoaderGeneration.Unknown, target.Generation);
        Assert.Equal(LoaderFlavor.Unknown, target.Flavor);
    }

    [Fact]
    public void EmptyOrNullReferencesAreNone()
    {
        Assert.Equal(LoaderTarget.None, LoaderTargetReader.FromReferences([]));
        Assert.Equal(LoaderTarget.None, LoaderTargetReader.FromReferences(null));
    }

    [Fact]
    public void DifferentGenerationsAreIncompatible()
    {
        var bep5 = new LoaderTarget(LoaderGeneration.BepInEx5, LoaderFlavor.Mono);
        var bep6 = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Mono);
        Assert.False(bep5.IsCompatibleWith(bep6));
    }

    [Fact]
    public void DifferentFlavorsAreIncompatible()
    {
        var mono = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Mono);
        var il2cpp = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Il2Cpp);
        Assert.False(mono.IsCompatibleWith(il2cpp));
    }

    [Fact]
    public void UnknownFlavorStaysCompatibleWithEither()
    {
        var core = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Unknown);
        var mono = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Mono);
        var il2cpp = new LoaderTarget(LoaderGeneration.BepInEx6, LoaderFlavor.Il2Cpp);
        Assert.True(core.IsCompatibleWith(mono));
        Assert.True(core.IsCompatibleWith(il2cpp));
    }
}
