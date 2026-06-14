using PeFix.Meta;

namespace PeFix.Tests;

public sealed class DepsReaderTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void ReadDeclaredAssets_ReturnsNull_WhenNoManifestPresent()
    {
        Assert.Null(DepsReader.ReadDeclaredAssets(_temp.DirPath));
    }

    [Fact]
    public void ReadDeclaredAssets_CollectsRuntimeAssetSimpleNames()
    {
        File.WriteAllText(Path.Combine(_temp.DirPath, "web.deps.json"), Manifest);

        IReadOnlySet<string>? assets = DepsReader.ReadDeclaredAssets(_temp.DirPath);

        Assert.NotNull(assets);
        Assert.Contains("web", assets);
        Assert.Contains("Newtonsoft.Json", assets);
        // Shared-framework assemblies are not application runtime assets.
        Assert.DoesNotContain("Microsoft.AspNetCore.Routing", assets);
    }

    [Fact]
    public void ReadDeclaredAssets_FallsBackToNull_WhenManifestMalformed()
    {
        File.WriteAllText(Path.Combine(_temp.DirPath, "broken.deps.json"), "{ not json");

        Assert.Null(DepsReader.ReadDeclaredAssets(_temp.DirPath));
    }

    private const string Manifest = """
        {
          "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" },
          "targets": {
            ".NETCoreApp,Version=v10.0": {
              "web/1.0.0": {
                "runtime": { "web.dll": {} }
              },
              "Newtonsoft.Json/13.0.3": {
                "runtime": { "lib/net6.0/Newtonsoft.Json.dll": { "assemblyVersion": "13.0.0.0", "fileVersion": "13.0.3.27908" } }
              }
            }
          },
          "libraries": {}
        }
        """;

    public void Dispose() => _temp.Dispose();
}
