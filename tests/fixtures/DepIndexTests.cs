using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class DepIndexTests
{
    [Fact]
    public void LookupFirstProviderWinsCaseInsensitive()
    {
        Inspection first = Make("Lib", "1.0.0.0", [], "/A.dll");
        Inspection second = Make("LIB", "2.0.0.0", [], "/B.dll");

        DepIndex deps = DepIndex.Build([first, second]);

        Assert.True(deps.TryGetProvider("lib", out Inspection found));
        Assert.Equal("/A.dll", found.Path);
    }

    [Fact]
    public void Missing_IgnoresProvidersAndPolicyProvidedRefs()
    {
        Inspection app = Make("App", "1.0.0.0", [
            Asm("Lib", "1.0.0.0"),
            Asm("System.Text.Json", "8.0.0.0"),
            Asm("Missing", "2.0.0.0"),
        ], "/App.dll");
        Inspection lib = Make("Lib", "1.0.0.0", [], "/Lib.dll");

        MissingRef[] missing = DepIndex.Build([app, lib]).FindMissing([app, lib]);

        MissingRef item = Assert.Single(missing);
        Assert.Equal("Missing", item.RefName);
        Assert.Equal("/App.dll", item.NeedBy);
    }

    [Fact]
    public void Conflicts_UseFirstProviderVersion()
    {
        Inspection app = Make("App", "1.0.0.0", [Asm("Lib", "1.0.0.0")], "/App.dll");
        Inspection lib = Make("Lib", "2.0.0.0", [], "/Lib.dll");

        VerConflict[] conflicts = DepIndex.Build([app, lib]).FindConflicts([app, lib]);

        VerConflict conflict = Assert.Single(conflicts);
        Assert.Equal("Lib", conflict.AssemblyName);
        Assert.Equal("1.0.0.0", conflict.Expected);
        Assert.Equal("2.0.0.0", conflict.Actual);
        Assert.Equal("/Lib.dll", conflict.ProvidedBy);
    }

    [Fact]
    public void Duplicates_GroupProvidersByAssemblyName()
    {
        Inspection first = Make("Lib", "1.0.0.0", [], "/B/Lib.dll");
        Inspection second = Make("lib", "1.0.0.0", [], "/A/Lib.dll");
        Inspection app = Make("App", "1.0.0.0", [], "/App.dll");

        DupProvider[] dupProviders = DepIndex.FindDuplicates([first, second, app]);

        DupProvider dup = Assert.Single(dupProviders);
        Assert.Equal("Lib", dup.AsmName);
        Assert.Equal(["/A/Lib.dll", "/B/Lib.dll"], dup.Files);
    }

    private static Inspection Make(string name, string version, AsmRef[] refs, string? path = null)
    {
        return new Inspection(
            path ?? $"/{name}.dll",
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
            refs,
            Asm(name, version));
    }

    private static AsmRef Asm(string name, string version) => new(name, version);
}
