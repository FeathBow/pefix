using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ClassifierR2rTests
{
    [Fact]
    public void ReadyToRunImageClassifiesAsR2rNotMixedMode()
    {
        // R2R clears IL-only (looks mixed-mode); guards the self-contained framework
        // misclassification that flagged every R2R assembly unsafe.
        PeSnapshot snapshot = R2rLike(new ReadyToRunInfo(9, 0));

        Inspection result = Classifier.Classify(snapshot);

        Assert.Equal(Category.R2R, result.Category);
        Assert.NotEqual(Status.Unsafe, result.Status);
    }

    [Fact]
    public void MixedModeWithoutR2rHeaderStillClassifiesAsMixedMode()
    {
        // Genuine C++/CLI has no R2R header, so the reorder must not weaken real detection.
        PeSnapshot snapshot = R2rLike(null);

        Inspection result = Classifier.Classify(snapshot);

        Assert.Equal(Category.MixedMode, result.Category);
    }

    private static PeSnapshot R2rLike(ReadyToRunInfo? readyToRun) => new(
        Path: "/pub/sample.dll",
        ValidPe: true,
        HasCliHeader: true,
        PeFormat: "PE32+",
        Machine: "Amd64",
        ManagedCorFlags: new ManagedCorFlags(IlOnly: false, Required32Bit: false, Preferred32Bit: false, Signed: true),
        Signals: new Signals(StrongName: true, HasPInvoke: false, IsRefAsm: false, IsMixedMode: true),
        ReadyToRun: readyToRun,
        AssemblyDefinition: new AssemblyIdentity("Sample", "1.0.0.0"));
}
