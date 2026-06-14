using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class TfmVersionTests
{
    // The TFM check is shape-based, not a version list, so every modern moniker including
    // ones newer than the installed SDK is handled without code change. Pure-logic proof of
    // version agnosticism; no SDK, runtime, or publish required.
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    [InlineData("net12.0")]
    public void ModernTfm_IsNotFlaggedLegacy(string tfm)
    {
        Assert.NotEqual(Category.TfmMismatch, Classifier.Classify(Snapshot(tfm)).Category);
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("net472")]
    [InlineData("net40")]
    public void LegacyFrameworkTfm_IsFlaggedTfmMismatch(string tfm)
    {
        Assert.Equal(Category.TfmMismatch, Classifier.Classify(Snapshot(tfm)).Category);
    }

    private static PeSnapshot Snapshot(string tfm) => new(
        Path: "/sample.dll",
        ValidPe: true,
        HasCliHeader: true,
        PeFormat: "PE32",
        Machine: "I386",
        ManagedCorFlags: new ManagedCorFlags(IlOnly: true, Required32Bit: false, Preferred32Bit: false, Signed: false),
        Signals: new Signals(StrongName: false, HasPInvoke: false, IsRefAsm: false, IsMixedMode: false),
        Tfm: tfm,
        AssemblyDefinition: new AssemblyIdentity("Sample", "1.0.0.0"));
}
