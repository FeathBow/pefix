using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ClosureTests
{
    [Fact]
    public void All_Resolved()
    {
        Inspection a = Make("A", "1.0", [Identity("B", "1.0")]);
        Inspection b = Make("B", "1.0", [Identity("C", "1.0")]);
        Inspection c = Make("C", "1.0", []);

        ClosureReport report = ClosureGraph.Build([a, b, c], "/test");

        Assert.Equal(3, report.Entries.Length);
        Assert.Empty(report.Unresolved);
        Assert.Empty(report.CycleChains);
        Assert.Equal(0, report.ProvidedLeaves.Total);
    }

    [Fact]
    public void Direct_Missing()
    {
        Inspection a = Make("A", "1.0", [Identity("Missing", "2.0")]);

        ClosureReport report = ClosureGraph.Build([a], "/test");

        Assert.Single(report.Unresolved);
        ClosureChain chain = report.Unresolved[0];
        Assert.Equal("A", chain.Entry.AssemblyName);
        Assert.Single(chain.Segments);
        Assert.Equal("Missing", chain.Segments[0].AssemblyName);
        Assert.Equal(ChainKind.Unresolved, chain.Segments[0].Kind);
        Assert.Equal(1, report.RefsWalked);
    }

    [Fact]
    public void Transitive_Missing()
    {
        Inspection a = Make("A", "1.0", [Identity("B", "1.0")]);
        Inspection b = Make("B", "1.0", [Identity("C", "1.0")]);
        Inspection c = Make("C", "1.0", [Identity("Missing", "1.0")]);

        ClosureReport report = ClosureGraph.Build([a, b, c], "/test");

        Assert.Equal(3, report.Entries.Length);
        Assert.Single(report.Unresolved, ch => ch.Entry.AssemblyName == "A");

        ClosureChain chain = report.Unresolved.First(ch => ch.Entry.AssemblyName == "A");
        Assert.Equal(3, chain.Segments.Length);
        Assert.Equal("B", chain.Segments[0].AssemblyName);
        Assert.Equal(ChainKind.Resolved, chain.Segments[0].Kind);
        Assert.Equal("C", chain.Segments[1].AssemblyName);
        Assert.Equal("Missing", chain.Segments[2].AssemblyName);
        Assert.Equal(ChainKind.Unresolved, chain.Segments[2].Kind);
    }

    [Fact]
    public void Cycle_Handled()
    {
        Inspection a = Make("A", "1.0", [Identity("B", "1.0")]);
        Inspection b = Make("B", "1.0", [Identity("A", "1.0")]);

        ClosureReport report = ClosureGraph.Build([a, b], "/test");

        Assert.NotEmpty(report.CycleChains);
        Assert.Empty(report.Unresolved);
        Assert.Equal(2, report.CycleChains.Length);
        Assert.Contains(report.CycleChains, ch => ch.Entry.AssemblyName == "A");
        Assert.Contains(report.CycleChains, ch => ch.Entry.AssemblyName == "B");
    }

    [Fact]
    public void Framework_NotReported()
    {
        Inspection a = Make("A", "1.0", [Identity("System", "4.0")]);

        ClosureReport report = ClosureGraph.Build([a], "/test");

        Assert.Empty(report.Unresolved);
        Assert.Equal(1, report.RefsWalked);
        Assert.Equal(new ProvidedLeafCounts(1, 1), report.ProvidedLeaves);
    }

    [Fact]
    public void Host_Reference_Is_ProvidedLeaf_Not_FrameworkLeaf()
    {
        Inspection a = Make("A", "1.0", [Identity("UnityEngine.CoreModule", "0.0.0.0")]);

        ClosureReport report = ClosureGraph.Build([a], "/test");

        Assert.Empty(report.Unresolved);
        Assert.Equal(1, report.RefsWalked);
        Assert.Equal(new ProvidedLeafCounts(1, 0), report.ProvidedLeaves);
    }

    [Fact]
    public void DiamondMissingDedupKeepsRepresentative()
    {
        Inspection a = Make("A", "1.0", [Identity("B", "1.0"), Identity("C", "1.0")]);
        Inspection b = Make("B", "1.0", [Identity("Missing", "1.0")]);
        Inspection c = Make("C", "1.0", [Identity("Missing", "1.0")]);

        ClosureReport report = ClosureGraph.Build([a, b, c], "/test");

        ClosureChain chain = Assert.Single(report.Unresolved, item => item.Entry.AssemblyName == "A");
        Assert.Equal("B", chain.Segments[0].AssemblyName);
        Assert.Equal("Missing", chain.Segments[^1].AssemblyName);
    }

    private static Inspection Make(string name, string ver, AssemblyIdentity[] references)
    {
        return new Inspection(
            $"{name}.dll",
            true,
            true,
            null,
            null,
            default,
            default,
            null,
            Status.Compatible,
            "test",
            "cause",
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            references,
            Identity(name, ver));
    }

    private static AssemblyIdentity Identity(string name, string version) => new(name, version);
}
