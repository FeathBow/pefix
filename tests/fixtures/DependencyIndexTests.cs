using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class DependencyIndexTests
{
    [Fact]
    public void LookupFirstProviderWinsCaseInsensitive()
    {
        Inspection first = Make("Lib", "1.0.0.0", [], "/A.dll");
        Inspection second = Make("LIB", "2.0.0.0", [], "/B.dll");

        DependencyIndex dependencies = DependencyIndex.Build([first, second]);

        Assert.True(dependencies.TryGetProvider("lib", out Inspection found));
        Assert.Equal("/A.dll", found.Path);
    }

    [Fact]
    public void Missing_IgnoresProvidersAndPolicyProvidedRefs()
    {
        Inspection app = Make("App", "1.0.0.0", [
            Identity("Lib", "1.0.0.0"),
            Identity("System.Text.Json", "8.0.0.0"),
            Identity("Missing", "2.0.0.0"),
        ], "/App.dll");
        Inspection lib = Make("Lib", "1.0.0.0", [], "/Lib.dll");

        MissingReference[] missing = DependencyIndex.Build([app, lib]).FindMissingReferences([app, lib]);

        MissingReference item = Assert.Single(missing);
        Assert.Equal("Missing", item.ReferenceName);
        Assert.Equal("/App.dll", item.RequiredBy);
    }

    [Fact]
    public void Missing_UsesSelectedHostProfileForProvidedLeaves()
    {
        HostProfile hostProfile = new(
            "test-host",
            new Dictionary<string, ProvidedKind>(StringComparer.OrdinalIgnoreCase)
            {
                ["HostOnly"] = ProvidedKind.Host,
            },
            []);
        Inspection app = Make("App", "1.0.0.0", [
            Identity("HostOnly", "1.0.0.0"),
            Identity("System", "8.0.0.0"),
        ], "/App.dll");

        MissingReference[] missing = DependencyIndex
            .Build([app], hostProfile)
            .FindMissingReferences([app]);

        MissingReference item = Assert.Single(missing);
        Assert.Equal("System", item.ReferenceName);
    }

    [Fact]
    public void Conflicts_UseFirstProviderVersion()
    {
        Inspection app = Make("App", "1.0.0.0", [Identity("Lib", "1.0.0.0")], "/App.dll");
        Inspection lib = Make("Lib", "2.0.0.0", [], "/Lib.dll");

        VersionConflict[] conflicts = DependencyIndex.Build([app, lib]).FindConflicts([app, lib]);

        VersionConflict conflict = Assert.Single(conflicts);
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

        DuplicateProvider[] duplicateProviders = DependencyIndex.FindDuplicateProviders([first, second, app]);

        DuplicateProvider duplicateProvider = Assert.Single(duplicateProviders);
        Assert.Equal("Lib", duplicateProvider.AssemblyName);
        Assert.Equal(["/A/Lib.dll", "/B/Lib.dll"], duplicateProvider.Files);
    }

    [Fact]
    public void Missing_SuppressesSharedFrameworkRefsViaDeclaredAssets()
    {
        // deps.json declares the application's own runtime assets {App, Lib, Restored}.
        // A reference outside that set is shared-framework provided (not missing); a
        // declared asset absent from the folder is the genuine missing case.
        Inspection app = Make("App", "1.0.0.0", [
            Identity("Lib", "1.0.0.0"),
            Identity("Microsoft.AspNetCore.Routing", "10.0.0.0"),
            Identity("Restored", "2.0.0.0"),
        ], "/App.dll");
        Inspection lib = Make("Lib", "1.0.0.0", [], "/Lib.dll");
        HashSet<string> declared = new(StringComparer.OrdinalIgnoreCase) { "App", "Lib", "Restored" };

        MissingReference[] missing = DependencyIndex
            .Build([app, lib], hostProfile: null, declaredAssets: declared)
            .FindMissingReferences([app, lib]);

        MissingReference item = Assert.Single(missing);
        Assert.Equal("Restored", item.ReferenceName);
    }

    [Fact]
    public void Conflicts_IgnoreZeroVersionFacadeReferences()
    {
        // A v0.0.0.0 reference (assembly facade / type-forward shim) is version-neutral
        // and must not conflict with the concrete in-folder provider it binds to.
        Inspection facade = Make("Facade", "10.0.0.0", [Identity("Real", "0.0.0.0")], "/Facade.dll");
        Inspection real = Make("Real", "10.0.0.0", [], "/Real.dll");

        VersionConflict[] conflicts = DependencyIndex.Build([facade, real]).FindConflicts([facade, real]);

        Assert.Empty(conflicts);
    }

    private static Inspection Make(string name, string version, AssemblyIdentity[] references, string? path = null)
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
            references,
            Identity(name, version));
    }

    private static AssemblyIdentity Identity(string name, string version) => new(name, version);
}
