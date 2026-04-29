using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Patch;

public static class PinvokeScan
{
    public static PinvokeRes Inspect(string path)
    {
        string fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var pe = new PEReader(stream);
        if (pe.PEHeaders.CorHeader is null)
            throw new InvalidOperationException("Not a managed assembly.");

        MetadataReader reader = pe.GetMetadataReader();
        List<PinvokeCall> calls = [];
        foreach (MethodDefinitionHandle h in reader.MethodDefinitions)
        {
            MethodDefinition md = reader.GetMethodDefinition(h);
            if (!md.Attributes.HasFlag(MethodAttributes.PinvokeImpl)) continue;
            MethodImport mi = md.GetImport();
            if (mi.Module.IsNil) continue;
            ModuleReference mr = reader.GetModuleReference(mi.Module);
            string moduleName = reader.GetString(mr.Name);
            string entryName = reader.GetString(mi.Name);
            string declType = TypeName(reader, md.GetDeclaringType());
            string methodName = reader.GetString(md.Name);
            calls.Add(new PinvokeCall(moduleName, declType, methodName, entryName));
        }
        return new PinvokeRes(fullPath, [.. calls]);
    }

    public static PinBatch InspectDir(string dir)
    {
        string fullDir = Path.GetFullPath(dir);
        List<PinvokeRes> results = [];
        List<Refusal> refusals = [];
        foreach (string dll in Directory.EnumerateFiles(fullDir, "*.dll"))
        {
            try
            {
                PinvokeRes r = Inspect(dll);
                if (r.Calls.Length > 0) results.Add(r);
            }
            catch (InvalidOperationException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
            catch (BadImageFormatException ex) { refusals.Add(Refusal.Create(dll, ex.Message)); }
        }
        return new PinBatch(fullDir, [.. results], [.. refusals]);
    }

    private static string TypeName(MetadataReader reader, TypeDefinitionHandle h)
    {
        TypeDefinition td = reader.GetTypeDefinition(h);
        string name = reader.GetString(td.Name);
        string typeNs = reader.GetString(td.Namespace);
        return string.IsNullOrEmpty(typeNs) ? name : $"{typeNs}.{name}";
    }
}
