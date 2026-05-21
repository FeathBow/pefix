using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class AttrReaderTests
{
    [Fact]
    public void InspectTrimmableAttributeRemainsParsed()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F12_trimmable.dll"));
        Assert.True(result.IsTrimmable);
    }

    [Fact]
    public void StripStrongNameWithoutSignedIvtRemainsClear()
    {
        using var temp = new TempDir();
        string path = temp.Copy("F03_x64_strongname.dll");
        SnStripResult result = SnStripper.Strip(path, new SnStripOpts(DryRun: true));
        Assert.False(result.HadSignedIvt);
    }
}
