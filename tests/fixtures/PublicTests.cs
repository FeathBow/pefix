using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PeFix.Patch;
using PeFix.Plan;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class PublicTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    private string CopyFix() => _temp.Copy("F19_internals.dll");

    [Fact]
    public void TypePub()
    {
        string path = CopyFix();
        PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        TypeAttributes vis = ReadType(path, "Foo") & TypeAttributes.VisibilityMask;
        Assert.Equal(TypeAttributes.Public, vis);
    }

    [Fact]
    public void MethPub()
    {
        string path = CopyFix();
        PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        MethodAttributes acc = ReadMeth(path, "Foo", "Get") & MethodAttributes.MemberAccessMask;
        Assert.Equal(MethodAttributes.Public, acc);
    }

    [Fact]
    public void FieldPub()
    {
        string path = CopyFix();
        PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        FieldAttributes acc = ReadField(path, "Foo", "_bar") & FieldAttributes.FieldAccessMask;
        Assert.Equal(FieldAttributes.Public, acc);
    }

    [Fact]
    public void SkipGen()
    {
        string path = CopyFix();
        PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        TypeAttributes stateVis = ReadType(path, "<Items>d__2") & TypeAttributes.VisibilityMask;
        Assert.Equal(TypeAttributes.NestedPrivate, stateVis);
    }

    [Fact]
    public void DrySame()
    {
        string path = CopyFix();
        byte[] before = File.ReadAllBytes(path);
        PublicResult r = PublicPatch.Publicize(path, new PubOptions(Backup: true, DryRun: true));
        Assert.True(r.WasDryRun);
        Assert.True(r.OpsCount > 0);
        Assert.All(r.Ops, op => Assert.True(op.Target.Row is > 0));
        Assert.Null(r.BackupPath);
        Assert.Null(r.PlanPath);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
    }

    [Fact]
    public void ApplyDiff()
    {
        string path = CopyFix();
        byte[] before = File.ReadAllBytes(path);
        PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        Assert.NotEqual(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void PlanOps()
    {
        string path = CopyFix();
        PublicResult result = PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        string sidecarPath = path + ".pefix-plan.json";
        Assert.Equal(sidecarPath, result.PlanPath);
        Assert.True(File.Exists(sidecarPath));
        PefixPlan plan = PlanJson.Read(File.ReadAllText(sidecarPath));
        Assert.NotEmpty(plan.Ops);
        Assert.All(plan.Ops, o => Assert.Equal("publicize.flag", o.Kind));
    }

    [Fact]
    public void PlanTargetsArePublicAfterApply()
    {
        string path = CopyFix();
        PublicResult result = PublicPatch.Publicize(path, new PubOptions(Backup: false, DryRun: false));
        PefixPlan plan = PlanJson.Read(File.ReadAllText(result.PlanPath!));

        PeRead.Meta(path, reader =>
        {
            Assert.All(plan.Ops, op => AssertPublicTarget(reader, op.Target));
            return 0;
        });
    }

    [Fact]
    public void VerifyFailRollsBack()
    {
        string path = CopyFix();
        byte[] before = File.ReadAllBytes(path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PublicPatch.Publicize(
                path,
                new PubOptions(Backup: true, DryRun: false),
                (_, _) => throw new InvalidOperationException("verify failed")));

        Assert.Contains("verify failed", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pefix-plan.json"));
        Assert.False(File.Exists(path + ".bak"));
    }

    private static void AssertPublicTarget(MetadataReader reader, PlanTarget target)
    {
        Assert.True(target.Row is > 0);
        int row = target.Row.Value;

        switch (target.Kind)
        {
            case "typedef.flags":
                AssertPublicType(reader, row);
                break;
            case "methoddef.flags":
                Assert.Equal(MethodAttributes.Public, ReadMethodAccess(reader, row));
                break;
            case "field.flags":
                Assert.Equal(FieldAttributes.Public, ReadFieldAccess(reader, row));
                break;
            default:
                throw new InvalidOperationException($"Unknown publicize target '{target.Kind}'.");
        }
    }

    private static void AssertPublicType(MetadataReader reader, int row)
    {
        TypeDefinition type = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row));
        TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
        Assert.True(visibility is TypeAttributes.Public or TypeAttributes.NestedPublic);
    }

    private static MethodAttributes ReadMethodAccess(MetadataReader reader, int row)
    {
        MethodDefinition method = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        return method.Attributes & MethodAttributes.MemberAccessMask;
    }

    private static FieldAttributes ReadFieldAccess(MetadataReader reader, int row)
    {
        FieldDefinition field = reader.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(row));
        return field.Attributes & FieldAttributes.FieldAccessMask;
    }

    private static TypeAttributes ReadType(string path, string typeName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (TypeDefinitionHandle h in reader.TypeDefinitions)
            {
                TypeDefinition td = reader.GetTypeDefinition(h);
                if (string.Equals(reader.GetString(td.Name), typeName, StringComparison.Ordinal))
                    return td.Attributes;
            }
            throw new InvalidOperationException($"Type '{typeName}' not found.");
        });
    }

    private static MethodAttributes ReadMeth(string path, string typeName, string methodName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (TypeDefinitionHandle th in reader.TypeDefinitions)
            {
                TypeDefinition td = reader.GetTypeDefinition(th);
                if (!string.Equals(reader.GetString(td.Name), typeName, StringComparison.Ordinal)) continue;
                foreach (MethodDefinitionHandle mh in td.GetMethods())
                {
                    MethodDefinition md = reader.GetMethodDefinition(mh);
                    if (string.Equals(reader.GetString(md.Name), methodName, StringComparison.Ordinal))
                        return md.Attributes;
                }
            }
            throw new InvalidOperationException($"Method '{typeName}.{methodName}' not found.");
        });
    }

    private static FieldAttributes ReadField(string path, string typeName, string fieldName)
    {
        return PeRead.Meta(path, reader =>
        {
            foreach (TypeDefinitionHandle th in reader.TypeDefinitions)
            {
                TypeDefinition td = reader.GetTypeDefinition(th);
                if (!string.Equals(reader.GetString(td.Name), typeName, StringComparison.Ordinal)) continue;
                foreach (FieldDefinitionHandle fh in td.GetFields())
                {
                    FieldDefinition fd = reader.GetFieldDefinition(fh);
                    if (string.Equals(reader.GetString(fd.Name), fieldName, StringComparison.Ordinal))
                        return fd.Attributes;
                }
            }
            throw new InvalidOperationException($"Field '{typeName}.{fieldName}' not found.");
        });
    }
}
