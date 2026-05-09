using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

public sealed class GateTests
{
    [Fact]
    public void Gate_Bad()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => GateEval.Meets(Status.Compatible, BadStatus()));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Map_Bad()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => InspectMap.ActionCode(FakeInspect(BadStatus())));
        Assert.Equal("result", ex.ParamName);
    }

    private static Status BadStatus()
    {
        int value = Enum.GetValues<Status>().Select(item => (int)item).Max() + 1;
        return (Status)value;
    }

    private static Inspection FakeInspect(Status status)
    {
        return new Inspection(
            "a.dll",
            true,
            true,
            "PE32",
            "I386",
            default,
            default,
            null,
            status,
            "portable",
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
            null);
    }
}
