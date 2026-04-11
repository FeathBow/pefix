using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ClassTests
{
    [Theory]
    [InlineData("F01_compatible_anycpu.dll", Category.ManagedPePortability, Status.Compatible)]
    [InlineData("F02_x64only_managed.dll", Category.ManagedPePortability, Status.Fixable)]
    [InlineData("F03_x64_strongname.dll", Category.ManagedPePortability, Status.FixableWithWarnings)]
    [InlineData("F04_x64_pinvoke.dll", Category.ManagedPePortability, Status.FixableWithWarnings)]
    [InlineData("F05_reference_assembly.dll", Category.ReferenceAssemblyMisuse, Status.Unsafe)]
    [InlineData("F06_mixed_mode.dll", Category.NonRewritableBinary, Status.Unsafe)]
    [InlineData("F07_native_pe.dll", Category.NonRewritableBinary, Status.Unsafe)]
    public void Inspect_Map(string fixture, Category category, Status status)
    {
        var result = PeAnalyzer.Inspect(FixturePaths.Get(fixture));
        Assert.Equal(category, result.Category);
        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData("F08_corrupt.dll")]
    [InlineData("F09_empty.dll")]
    public void Inspect_Bad(string fixture)
    {
        var result = PeAnalyzer.Inspect(FixturePaths.Get(fixture));
        Assert.Equal(Status.Corrupt, result.Status);
    }
}
