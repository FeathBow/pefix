using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class RefInventoryTests
{
    [Fact]
    public void Collect_MapsPresentMissingConflictAndHostProvided()
    {
        Inspection app = Assembly(Spec("App", "1.0.0.0", [
            Ref("Lib", "1.0.0.0"),
            Ref("Missing", "2.0.0.0"),
            Ref("Conflict", "1.0.0.0"),
            Ref("System.Runtime", "10.0.0.0"),
        ]));
        Inspection lib = Assembly(Spec("Lib", "1.0.0.0", []));
        Inspection conflict = Assembly(Spec("Conflict", "2.0.0.0", []));

        RefEntry[] entries = RefInventory.Collect(
            [app, lib, conflict],
            HostProfile.Default);

        AssertEntry(entries, "Lib", RefStatus.Present, "/Lib.dll", "1.0.0.0");
        AssertEntry(entries, "Missing", RefStatus.Missing, null, null);
        AssertEntry(entries, "Conflict", RefStatus.VersionConflict, "/Conflict.dll", "2.0.0.0");
        AssertEntry(entries, "System.Runtime", RefStatus.HostProvided, null, null);
    }

    [Fact]
    public void Collect_TreatsZeroVersionFacadeRefAsPresent()
    {
        // A v0.0.0.0 reference is version-neutral; the inventory must report it Present,
        // consistent with the conflict gate, not VersionConflict.
        Inspection app = Assembly(Spec("App", "1.0.0.0", [Ref("Facade", "0.0.0.0")]));
        Inspection facade = Assembly(Spec("Facade", "10.0.0.0", []));

        RefEntry[] entries = RefInventory.Collect([app, facade], HostProfile.Default);

        AssertEntry(entries, "Facade", RefStatus.Present, "/Facade.dll", "10.0.0.0");
    }

    private static void AssertEntry(
        RefEntry[] entries,
        string name,
        RefStatus status,
        string? providerPath,
        string? providerVersion)
    {
        RefEntry entry = Assert.Single(entries, item => item.ReferenceName == name);
        Assert.Equal(status, entry.Status);
        Assert.Equal($"/App.dll", entry.ConsumerPath);
        Assert.Equal(providerPath, entry.ProviderPath);
        Assert.Equal(providerVersion, entry.ProviderVersion);
    }

    private static Inspection Assembly(TestAssembly spec)
    {
        return new Inspection(
            $"/{spec.Name}.dll",
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
            spec.References,
            Ref(spec.Name, spec.Version));
    }

    private static TestAssembly Spec(
        string name,
        string version,
        AssemblyIdentity[] references)
    {
        return new TestAssembly
        {
            Name = name,
            Version = version,
            References = references
        };
    }

    private static AssemblyIdentity Ref(string name, string version)
    {
        return new AssemblyIdentity(name, version);
    }

    private sealed class TestAssembly
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public required AssemblyIdentity[] References { get; init; }
    }
}
