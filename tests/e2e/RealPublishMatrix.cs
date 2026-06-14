using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PeFix.Tests;

// Builds REAL `dotnet publish` artifacts once for the precision suite, so shared-framework
// / R2R / native regressions are caught against real deployment shapes, not synthetic
// inputs. Needs the SDK + network; when dotnet is absent Cases is empty and tests skip.
public sealed class RealPublishMatrix : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "pefix-precision-" + Guid.NewGuid().ToString("N")[..8]);

    public RealPublishMatrix()
    {
        if (!DotnetCli.Available)
            return;

        Directory.CreateDirectory(_root);
        string rid = PortableRid();

        // Framework-dependent shapes: shared-framework refs must NOT be flagged missing.
        // con uses its package so the negative control has a real reference to break.
        Console("con", ["Newtonsoft.Json"], rid, removableDep: "Newtonsoft.Json.dll", program: UsesNewtonsoft,
            Pub("console-fdd-nuget"));
        Console("conm", ["Serilog", "Newtonsoft.Json"], rid, removableDep: null, program: null,
            Pub("console-multipkg"));
        Template("web", "web", rid, Pub("aspnetcore-fdd"));
        Template("api", "webapi", rid, Pub("aspnetcore-webapi"));
        Template("mvc", "mvc", rid, Pub("aspnetcore-mvc"));
        Template("grpc", "grpc", rid, Pub("grpc-fdd"));
        Template("wrk", "worker", rid, Pub("worker-fdd"));
        Template("lib", "classlib", rid, Pub("classlib-fdd"));

        // EF Core pulls a large managed closure plus a native sqlite asset under
        // runtimes/<rid>/native/ - exercises native resolution and RID-fallback layout.
        Console("ef", ["Microsoft.EntityFrameworkCore.Sqlite"], rid, removableDep: null, program: UsesEfCore,
            Pub("efcore-fdd"), Pub("efcore-selfcontained", selfContained: true));

        // Self-contained shapes: framework assemblies are R2R + native, present in-folder.
        Template("web", "web", rid, Pub("aspnetcore-selfcontained", selfContained: true));
        Console("consf", [], rid, removableDep: null, program: null,
            Pub("console-singlefile", selfContained: true, singleFile: true));
    }

    private readonly List<PublishCase> _cases = [];

    public IReadOnlyList<PublishCase> Cases => _cases;

    private static PublishSpec Pub(string name, bool selfContained = false, bool singleFile = false) =>
        new(name, selfContained, singleFile);

    private const string UsesNewtonsoft =
        "System.Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new { ok = 1 }));\n";

    private const string UsesEfCore =
        "using Microsoft.EntityFrameworkCore;\n" +
        "var options = new DbContextOptionsBuilder().UseSqlite(\"Data Source=app.db\").Options;\n" +
        "System.Console.WriteLine(options is not null);\n";

    private void Console(string proj, string[] packages, string rid, string? removableDep, string? program, params PublishSpec[] specs)
    {
        string projDir = Path.Combine(_root, proj);
        if (!Directory.Exists(projDir) && !Scaffold("console", proj, packages))
            return;

        if (program is not null)
            File.WriteAllText(Path.Combine(projDir, "Program.cs"), program);

        foreach (PublishSpec spec in specs)
            PublishCaseInto(proj, rid, spec, removableDep);
    }

    private void Template(string proj, string template, string rid, params PublishSpec[] specs)
    {
        string projDir = Path.Combine(_root, proj);
        if (!Directory.Exists(projDir) && !Scaffold(template, proj, []))
            return;

        foreach (PublishSpec spec in specs)
            PublishCaseInto(proj, rid, spec, removableDep: null);
    }

    private bool Scaffold(string template, string proj, string[] packages)
    {
        if (DotnetCli.Run(_root, "new", template, "-o", proj, "--no-restore").ExitCode != 0)
            return false;

        string projDir = Path.Combine(_root, proj);
        foreach (string package in packages)
        {
            if (DotnetCli.Run(projDir, "add", "package", package).ExitCode != 0)
                return false;
        }

        return true;
    }

    private void PublishCaseInto(string proj, string rid, PublishSpec spec, string? removableDep)
    {
        string projDir = Path.Combine(_root, proj);
        string outDir = Path.Combine(_root, "pub", spec.Name);
        List<string> args = ["publish", "-c", "Release", "-o", outDir];
        if (spec.SelfContained)
        {
            args.AddRange(["-r", rid, "--self-contained"]);
            if (spec.SingleFile)
                args.Add("-p:PublishSingleFile=true");
        }

        if (DotnetCli.Run(projDir, [.. args]).ExitCode != 0)
            return;

        _cases.Add(new PublishCase(spec.Name, outDir, removableDep));
    }

    private static string PortableRid()
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : "linux";
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64"
        };
        return $"{os}-{arch}";
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private readonly record struct PublishSpec(string Name, bool SelfContained, bool SingleFile);
}

// RemovableDep, when set, is an in-folder NuGet assembly the negative test deletes to
// prove the gate fires on a genuinely missing dependency.
public sealed record PublishCase(string Name, string Dir, string? RemovableDep);

[CollectionDefinition("real-publish")]
public sealed class RealPublishCollection : ICollectionFixture<RealPublishMatrix>;
