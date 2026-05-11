using PeFix.Meta;
using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class AttrReaderTests
{
    [Fact]
    public void Inspect_TrimmableAttribute_RemainsParsed()
    {
        var result = PeAnalyzer.Inspect(Paths.Get("F12_trimmable.dll"));
        Assert.True(result.IsTrimmable);
    }

    [Fact]
    public void Strip_StrongNameWithoutSignedIvt_RemainsClear()
    {
        using var temp = new TempDir();
        string path = temp.Copy("F03_x64_strongname.dll");
        SnStripRes result = SnStripper.Strip(path, new SnStripOpts(DryRun: true));
        Assert.False(result.HadSignedIvt);
    }
}
