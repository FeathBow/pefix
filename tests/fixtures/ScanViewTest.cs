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
        ScanResult scan = ScanBuild.Build(new ScanReport(
            Root,
            [Fixable("mods/Fix.dll"), Compatible("mods/Ok.dll")],
            [new VersionConflict("Dependency", "1.0.0.0", "2.0.0.0", Abs("mods/Fix.dll"), Abs("providers/Dependency.dll"))],
            [new MissingReference("System.Text.Json", "8.0.0.0", Abs("mods/Fix.dll"))],
            [new DuplicateProvider("Newtonsoft.Json", [Abs("plugins/A/Newtonsoft.Json.dll"), Abs("plugins/B/Newtonsoft.Json.dll")])],
            []),
            withJson: true);
        ScanView view = scan.View;
        ScanJsonParts json = scan.Json ?? throw new InvalidOperationException("JSON context was not built.");

        Assert.True(view.Stats.HasFixable);
        Assert.True(view.Stats.HasConflict);
        Assert.Equal(4, view.Stats.NeedCount);
        Assert.Equal("mods/Fix.dll", view.Conflicts[0].ReferencedBy);
        Assert.Equal("providers/Dependency.dll", view.Conflicts[0].ProvidedBy);
        Assert.Equal("mods/Fix.dll", view.Files[0].ViewPath);
        Assert.Equal("portability", view.Files[0].Category);
        Assert.Equal(Status.Fixable, view.Files[0].Status);
        Assert.Equal("non_portable", view.Files[0].ReasonCode);
        Assert.Equal("fix", view.Files[0].ActionText);
        Assert.Contains("platform-specific header", view.Files[0].ReasonText);

        using JsonDocument doc = JsonDocument.Parse(JsonWriter.Render(view, json));
        JsonElement root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("issues").GetArrayLength());
        Assert.Equal("fail", root.GetProperty("gate").GetProperty("integrity").GetString());
        JsonElement conflict = Assert.Single(root.GetProperty("conflicts").EnumerateArray());
        JsonElement fixResult = JsonAssert.SingleBy(root.GetProperty("results"), "reason_code", "non_portable");
        Assert.Equal("mods/Fix.dll", conflict.GetProperty("referenced_by").GetString());
        Assert.Equal("fix", fixResult.GetProperty("action").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_action").GetProperty("fix").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("by_action").GetProperty("none").GetInt32());
        Assert.Equal(3, root.GetProperty("gate").GetProperty("issue_count").GetInt32());
        Assert.Equal(
            ["asm_conflict", "dup_provider", "missing_ref"],
            JsonAssert.StringArray(root.GetProperty("gate").GetProperty("issue_codes")));

        string text = ScanOut.Render(view);
        Assert.Contains("mods/Fix.dll [fixable] reason=non_portable action=fix", text);
        Assert.Contains("Remove the mismatched copy or install the version required by the referencing assembly", text);
        Assert.Contains("Blocking Issues (3):", text);
        Assert.Contains("[asm_conflict] Dependency", text);
        Assert.Contains("repair: assisted_fix", text);
        Assert.Contains("verify: pefix scan <path> --json", text);
        Assert.Contains("Static Boundary: Findings are static evidence only", text);
        Assert.True(
            text.IndexOf("Blocking Issues", StringComparison.Ordinal) <
            text.IndexOf("Group: portability", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_TextStatesPassingStaticBoundary()
    {
        ScanResult scan = ScanBuild.Build(new ScanReport(
            Root,
            [Compatible("mods/Ok.dll")],
            [],
            [],
            [],
            []),
            withJson: false);
        ScanView view = scan.View;

        string text = ScanOut.Render(view);

        Assert.Contains("Blocking Issues: none found under supported static checks.", text);
        Assert.Contains("Runtime load success is not certified.", text);
    }

    [Fact]
    public void Scan_TextCallsOutBlockingFileDiagnostics()
    {
        ScanResult scan = ScanBuild.Build(new ScanReport(
            Root,
            [NewInspect("mods/Ref.dll", Status.Unsafe, "ref_assembly")],
            [],
            [],
            [],
            []),
            withJson: false);

        string text = ScanOut.Render(scan.View);

        Assert.Contains("Action:  Resolve blocking file diagnostics below", text);
        Assert.Contains("Blocking Issues: blocking file diagnostics are listed below.", text);
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
