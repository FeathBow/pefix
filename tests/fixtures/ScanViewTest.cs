using System.Text.Json;
using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Tests;

public sealed class ScanViewTest
{
    private const string Root = "/scan-root";

    [Fact]
    public void Scan_Parity()
    {
        ScanView view = ScanBuild.Build(new ScanReport(
            Root,
            [Fixable("mods/Fix.dll"), Compatible("mods/Ok.dll")],
            [new VerConflict("Dependency", "1.0.0.0", "2.0.0.0", Abs("mods/Fix.dll"), Abs("providers/Dependency.dll"))],
            [new MissingRef("System.Text.Json", "8.0.0.0", Abs("mods/Fix.dll"))],
            [new DupProvider("Newtonsoft.Json", [Abs("plugins/A/Newtonsoft.Json.dll"), Abs("plugins/B/Newtonsoft.Json.dll")])]),
            withJson: true);

        Assert.True(view.Stats.HasFixable);
        Assert.True(view.Stats.HasConflict);
        Assert.Equal(4, view.Stats.NeedCount);
        Assert.Equal("mods/Fix.dll", view.Conflicts[0].ReferencedBy);
        Assert.Equal("providers/Dependency.dll", view.Conflicts[0].ProvidedBy);
        Assert.Equal("mods/Fix.dll", view.Files[0].ViewPath);
        Assert.Equal("portability", view.Files[0].Category);
        Assert.Equal(Status.Fixable, view.Files[0].Status);
        Assert.Equal("non_portable", view.Files[0].ReasonCode);
        Assert.Equal("fix", view.Files[0].Action);
        Assert.Contains("platform-specific header", view.Files[0].Why);

        using JsonDocument doc = JsonDocument.Parse(JsonWriter.Render(view));
        JsonElement root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("issues").GetArrayLength());
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        Assert.Equal("mods/Fix.dll", root.GetProperty("conflicts")[0].GetProperty("referenced_by").GetString());
        Assert.Equal("non_portable", root.GetProperty("results")[0].GetProperty("reason_code").GetString());
        Assert.Equal("fix", root.GetProperty("results")[0].GetProperty("action").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_action").GetProperty("fix").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_action").GetProperty("none").GetInt32());
        Assert.Equal(3, root.GetProperty("gate").GetProperty("issue_count").GetInt32());

        string text = ScanOut.Render(view);
        Assert.Contains("mods/Fix.dll [fixable] reason=non_portable action=fix", text);
        Assert.Contains("Align the directory to a single version for this assembly name", text);
    }

    private static Inspection Compatible(string path)
    {
        return NewInspect(path, Status.Compatible, "portable");
    }

    private static Inspection Fixable(string path)
    {
        return NewInspect(path, Status.Fixable, "non_portable");
    }

    private static Inspection NewInspect(string path, Status status, string reasonCode)
    {
        return new Inspection(
            Abs(path),
            true,
            true,
            "PE32",
            "I386",
            default,
            default,
            Category.Portability,
            status,
            reasonCode,
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

    private static string Abs(string path)
    {
        return Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
