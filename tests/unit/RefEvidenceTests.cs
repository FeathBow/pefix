using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class RefEvidenceTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Collect_MapsMissingReferences()
    {
        _temp.CopyAll("F18_missing_refs.dll");
        Inspection[] inspections = Inspect();
        MissingReference[] expected = Dependencies(inspections).FindMissingReferences(inspections);

        RefFinding[] actual = Findings(inspections, RefOutcome.Missing);

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(
            expected.Select(item => (item.ReferenceName, item.RequiredVersion, item.RequiredBy)),
            actual.Select(item => (item.ReferenceName, item.ExpectedVersion!, item.ConsumerPath)));
        Assert.All(actual, AssertAssemblyGate);
    }

    [Fact]
    public void Collect_MapsVersionConflicts()
    {
        _temp.CopyAll("F01_compatible_anycpu.dll", "F17_conflict.dll");
        Inspection[] inspections = Inspect();
        VersionConflict[] expected = Dependencies(inspections).FindConflicts(inspections);

        RefFinding[] actual = Findings(inspections, RefOutcome.VersionConflict);

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(
            expected.Select(item => (item.AssemblyName, item.Expected, item.Actual, item.ReferencedBy, item.ProvidedBy)),
            actual.Select(item => (item.ReferenceName, item.ExpectedVersion!, item.ActualVersion!, item.ConsumerPath, item.ProviderPath!)));
        Assert.All(actual, AssertAssemblyGate);
    }

    [Fact]
    public void Collect_MapsDuplicateProviders()
    {
        CopyDuplicateProviders();
        Inspection[] inspections = Inspect();
        DuplicateProvider[] expected = DependencyIndex.FindDuplicateProviders(inspections);

        RefFinding[] actual = Findings(inspections, RefOutcome.DuplicateProvider);

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected.Select(item => item.AssemblyName), actual.Select(item => item.ReferenceName));
        Assert.Equal(expected.Select(item => item.Files), actual.Select(item => item.ProviderPaths!));
        Assert.All(actual, item => Assert.Equal(RefTier.Provider, item.Tier));
        Assert.All(actual, item => Assert.Equal(Confidence.Gate, item.Confidence));
    }

    [Fact]
    public void Collect_MapsMemberGaps()
    {
        _temp.Copy("F36_member_consumer.dll");
        File.Copy(Paths.Get("F34_member_provider_thin.dll"), Path.Combine(_temp.DirPath, "MemberProvider.dll"));
        Inspection[] inspections = Inspect();
        DependencyIndex dependencies = Dependencies(inspections);
        MemberRefGap[] expected = MemberSurfaceAnalyzer.FindMethodGaps(inspections, dependencies);

        RefFinding[] actual = Findings(inspections, RefOutcome.MemberGap);

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(
            expected.Select(item => (item.AssemblyName, item.TypeName, item.MemberName, item.ParameterCount, item.ConsumerPath, item.ProviderPath)),
            actual.Select(item => (item.ReferenceName, item.TypeName!, item.MemberName!, item.ParameterCount!.Value, item.ConsumerPath, item.ProviderPath!)));
        Assert.All(actual, item => Assert.Equal(RefTier.MemSurface, item.Tier));
        Assert.All(actual, item => Assert.Equal(Confidence.Gate, item.Confidence));
    }

    public void Dispose() => _temp.Dispose();

    private Inspection[] Inspect()
    {
        return Scanner.InspectDir(_temp.DirPath).Results;
    }

    private RefFinding[] Findings(Inspection[] inspections, RefOutcome resolution)
    {
        return [.. RefEvidence
            .Collect(inspections, HostProfile.DotNet)
            .Where(item => item.Resolution == resolution)];
    }

    private static DependencyIndex Dependencies(Inspection[] inspections)
    {
        return DependencyIndex.Build(inspections, HostProfile.DotNet);
    }

    private void CopyDuplicateProviders()
    {
        string source = Paths.Get("F01_compatible_anycpu.dll");
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginA.dll"));
        File.Copy(source, Path.Combine(_temp.DirPath, "PluginB.dll"));
    }

    private static void AssertAssemblyGate(RefFinding finding)
    {
        Assert.Equal(RefTier.AssemblyRef, finding.Tier);
        Assert.Equal(Confidence.Gate, finding.Confidence);
    }
}
