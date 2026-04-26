using System.Text.Json;
using PeFix.Plan;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class PlanTests
{
    private static PefixPlan MakePlan() => new(
        Version: 1,
        Tool: new PlanTool("pefix", "0.1.0"),
        Inputs: [new PlanFile("in.dll", new string('a', 64), 1024, "00000000-0000-0000-0000-000000000001")],
        Ops: [new MutationOp(
            "pe.header",
            new PlanTarget("pe.offset", Offset: 4),
            "0200",
            "0100")],
        Outputs: [new PlanFile("in.dll", new string('b', 64), 1024, "00000000-0000-0000-0000-000000000002")],
        Rollback: new PlanRollback("bak", JsonDocument.Parse("\"in.dll.bak\"").RootElement),
        Provenance: new PlanMeta(Sha: null, Host: "ci-host", Ts: new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void RoundTrip()
    {
        var plan = MakePlan();
        var json = PlanJson.Write(plan);
        var back = PlanJson.Read(json);

        Assert.Equal(plan.Version, back.Version);
        Assert.Equal(plan.Tool.Name, back.Tool.Name);
        Assert.Equal(plan.Tool.Version, back.Tool.Version);
        Assert.Single(back.Inputs);
        Assert.Equal(plan.Inputs[0].Path, back.Inputs[0].Path);
        Assert.Equal(plan.Inputs[0].Sha256, back.Inputs[0].Sha256);
        Assert.Equal(plan.Inputs[0].Size, back.Inputs[0].Size);
        Assert.Equal(plan.Inputs[0].Mvid, back.Inputs[0].Mvid);
        Assert.Single(back.Ops);
        Assert.Equal(plan.Ops[0].Kind, back.Ops[0].Kind);
        Assert.Equal(plan.Ops[0].Before, back.Ops[0].Before);
        Assert.Equal(plan.Ops[0].After, back.Ops[0].After);
        Assert.Equal(plan.Ops[0].Target.Kind, back.Ops[0].Target.Kind);
        Assert.Equal(plan.Ops[0].Target.Offset, back.Ops[0].Target.Offset);
        Assert.Equal(plan.Rollback.Kind, back.Rollback.Kind);
        Assert.Equal(plan.Provenance.Host, back.Provenance.Host);
        Assert.Equal(plan.Provenance.Ts, back.Provenance.Ts);
        Assert.Null(back.Provenance.Sha);
        Assert.Null(back.Provenance.Url);
    }

    [Fact]
    public void JsonKeys()
    {
        var json = PlanJson.Write(MakePlan());
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"tool\"", json);
        Assert.Contains("\"inputs\"", json);
        Assert.Contains("\"ops\"", json);
        Assert.Contains("\"outputs\"", json);
        Assert.Contains("\"rollback\"", json);
        Assert.Contains("\"provenance\"", json);
    }

    [Fact]
    public void NullJson()
    {
        Assert.Throws<JsonException>(() => PlanJson.Read("null"));
    }

    [Fact]
    public void MultiOps()
    {
        var plan = MakePlan() with
        {
            Ops =
            [
                new MutationOp("pe.header", new PlanTarget("pe.offset", Offset: 0), "AA", "BB"),
                new MutationOp("snstrip",   new PlanTarget("corflags"),              "0300", "0100"),
                new MutationOp("redir.version", new PlanTarget("asmref", Table: "AssemblyRef", Row: 3), "09000000", "0D000000"),
            ]
        };
        var back = PlanJson.Read(PlanJson.Write(plan));
        Assert.Equal(3, back.Ops.Length);
        Assert.Equal("snstrip", back.Ops[1].Kind);
        Assert.Equal("AssemblyRef", back.Ops[2].Target.Table);
        Assert.Equal(3, back.Ops[2].Target.Row);
    }

    [Fact]
    public void NilTarget()
    {
        var op = new MutationOp("pe.header", new PlanTarget("pe.offset"), "00", "01");
        var plan = MakePlan() with { Ops = [op] };
        var back = PlanJson.Read(PlanJson.Write(plan));
        var t = back.Ops[0].Target;
        Assert.Null(t.Table);
        Assert.Null(t.Row);
        Assert.Null(t.Handle);
        Assert.Null(t.Offset);
    }

    [Fact]
    public void FullMeta()
    {
        var url = new Uri("https://ci.example.com/run/42");
        var prov = new PlanMeta("abc123", "build-host", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), url);
        var plan = MakePlan() with { Provenance = prov };
        var back = PlanJson.Read(PlanJson.Write(plan));
        Assert.Equal("abc123", back.Provenance.Sha);
        Assert.Equal(url, back.Provenance.Url);
    }

    [Fact]
    public void VerOne()
    {
        var json = PlanJson.Write(MakePlan());
        Assert.Contains("\"version\": 1", json);
    }

    [Fact]
    public void NoOps()
    {
        var plan = MakePlan() with { Ops = [] };
        var back = PlanJson.Read(PlanJson.Write(plan));
        Assert.Empty(back.Ops);
    }
}
