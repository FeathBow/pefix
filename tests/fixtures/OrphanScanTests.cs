using PeFix.Meta;

namespace PeFix.Tests;

public sealed class OrphanScanTests
{
    [Fact]
    public void FindOrphans_FlagsUnreferencedLibrary()
    {
        Inspection lib = Make("/d/lib.dll", "Lib", [new AssemblyIdentity("Dep", "1.0.0.0")]);
        Inspection dep = Make("/d/dep.dll", "Dep", []);

        string[] orphans = OrphanScan.FindOrphans([lib, dep]);

        Assert.Equal(["/d/lib.dll"], orphans);
    }

    [Fact]
    public void FindOrphans_SkipsEntryPointAndSatellite()
    {
        Inspection app = Make("/d/app.dll", "App", []) with { HasEntryPoint = true };
        Inspection satellite = Make("/d/x.resources.dll", "X.resources", []);

        Assert.Empty(OrphanScan.FindOrphans([app, satellite]));
    }

    private static Inspection Make(string path, string name, AssemblyIdentity[] refs)
    {
        return new Inspection(
            path,
            true,
            true,
            "PE32",
            "I386",
            default,
            default,
            Category.Portability,
            Status.Compatible,
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
            refs,
            new AssemblyIdentity(name, "1.0.0.0"));
    }
}
