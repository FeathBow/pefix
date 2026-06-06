using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class ReflScannerTests
{
    [Fact]
    public void Scan_DecodesAllFixtureMethodsWithoutDesync()
    {
        string fixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");
        Inspection[] inspections = Scanner.InspectDir(fixtureDir).Results;

        ReflScan result = ReflScanner.Scan(inspections, HostProfile.DotNet);

        Assert.Equal(0, result.DesyncMethodCount);
    }

    [Fact]
    public void Parser_ExtractsAssemblyNameFromTypeQualifiedName()
    {
        bool parsed = AsmNameParse.TryParse(
            "Plugin.Namespace.Type, ReflectedAssembly, Version=1.0.0.0",
            requireComma: true,
            out string assemblyName);

        Assert.True(parsed);
        Assert.Equal("ReflectedAssembly", assemblyName);
    }

    [Fact]
    public void Scan_SkipsReflectionReferencesFromProvidedConsumer()
    {
        Inspection plugin = PeAnalyzer.Inspect(Paths.Get("F39_reflection_missing.dll"));
        Inspection framework = plugin with
        {
            AssemblyDefinition = new AssemblyIdentity("mscorlib", "4.0.0.0")
        };

        ReflScan pluginResult = ReflScanner.Scan([plugin], HostProfile.DotNet);
        ReflScan frameworkResult = ReflScanner.Scan([framework], HostProfile.DotNet);

        Assert.Single(pluginResult.References);
        Assert.Empty(frameworkResult.References);
    }
}
