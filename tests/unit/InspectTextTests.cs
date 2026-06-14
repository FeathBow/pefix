using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class InspectTextTests
{
    [Theory]
    [InlineData("F07_native_pe.dll")]
    [InlineData("F11_r2r.dll")]
    [InlineData("F12_trimmable.dll")]
    [InlineData("F13_bundle.dll")]
    public void Summary_IsThePreciseReasonCause_NotGenericStatusText(string fixture)
    {
        Inspection result = PeAnalyzer.Inspect(Paths.Get(fixture));

        // Cautioned spans many distinct reasons; the "why" must be the precise per-reason
        // cause, never a generic status-keyed line that is wrong for most of them.
        Assert.Equal(result.PrimaryCause, InspectText.Summary(result));
        Assert.DoesNotContain("can be fixed", InspectText.Summary(result), StringComparison.Ordinal);
    }
}
