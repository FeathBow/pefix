using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

public static class PeAnalyzer
{
    public static Inspection Inspect(string path)
    {
        string fullPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File not found.", fullPath);
        }

        if (fileInfo.Length == 0)
        {
            return Classifier.CreateBad(fullPath, "The file is empty.");
        }

        try
        {
            using var mmf = MemoryMappedFile.CreateFromFile(fullPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using MemoryMappedViewStream stream = mmf.CreateViewStream(0, fileInfo.Length, MemoryMappedFileAccess.Read);
            using var peReader = new PEReader(stream);
            PeSnapshot snapshot = ReadSnapshot(fullPath, peReader);
            return Classifier.Classify(snapshot);
        }
        catch (BadImageFormatException)
        {
            return Classifier.CreateBad(fullPath, "This file is not a valid PE file or is corrupted.");
        }
    }

    private static PeSnapshot ReadSnapshot(string path, PEReader peReader)
    {
        PEHeaders headers = peReader.PEHeaders;
        PEHeader? peHeader = headers.PEHeader;
        if (peHeader is null)
        {
            return new PeSnapshot(path, false, false, null, null, default, default);
        }

        string peFormat = PeFormat(peHeader.Magic);
        string machine = MachineText(headers.CoffHeader.Machine);
        CorHeader? corHeader = headers.CorHeader;
        if (corHeader is null)
        {
            return new PeSnapshot(path, true, false, peFormat, machine, default, default);
        }

        CorFlags corFlags = corHeader.Flags;
        MetadataReader reader = peReader.GetMetadataReader();
        var flags = new CliFlags(
            corFlags.HasFlag(CorFlags.ILOnly),
            corFlags.HasFlag(CorFlags.Requires32Bit),
            corFlags.HasFlag(CorFlags.Prefers32Bit),
            corFlags.HasFlag(CorFlags.StrongNameSigned));
        var signals = new Signals(
            flags.Signed || corHeader.StrongNameSignatureDirectory.Size > 0,
            HasPInvoke(reader),
            IsRefAsm(reader),
            !flags.IlOnly);

        string[]? pinvokeDeps = ReadPInvokes(reader);

        return new PeSnapshot(path, true, true, peFormat, machine, flags, signals, pinvokeDeps);
    }

    private static string[]? ReadPInvokes(MetadataReader reader)
    {
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);
            if (!method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            {
                continue;
            }

            MethodImport import = method.GetImport();
            if (import.Module.IsNil)
            {
                continue;
            }

            ModuleReference moduleRef = reader.GetModuleReference(import.Module);
            string moduleName = reader.GetString(moduleRef.Name).ToLowerInvariant();
            if (!string.IsNullOrEmpty(moduleName))
            {
                modules.Add(moduleName);
            }
        }

        return modules.Count > 0 ? [.. modules.OrderBy(m => m, StringComparer.Ordinal)] : null;
    }

    private static bool HasPInvoke(MetadataReader reader)
    {
        return reader.MethodDefinitions.Any(handle =>
            reader.GetMethodDefinition(handle).Attributes.HasFlag(MethodAttributes.PinvokeImpl));
    }

    private static bool IsRefAsm(MetadataReader reader)
    {
        AssemblyDefinition assembly = reader.GetAssemblyDefinition();
        return assembly.GetCustomAttributes().Any(handle =>
            IsRefAsm(reader, reader.GetCustomAttribute(handle)));
    }

    private static bool IsRefAsm(MetadataReader reader, CustomAttribute attribute)
    {
        return attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => MatchesType(reader, reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
            HandleKind.MethodDefinition => MatchesType(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => false
        };
    }

    private static bool MatchesType(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeReference:
                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                return MatchesType(reader, typeRef.Namespace, typeRef.Name);
            case HandleKind.TypeDefinition:
                TypeDefinition typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return MatchesType(reader, typeDef.Namespace, typeDef.Name);
            default:
                return false;
        }
    }

    private static bool MatchesType(MetadataReader reader, StringHandle nsHandle, StringHandle nameHandle)
    {
        string nsValue = reader.GetString(nsHandle);
        string typeName = reader.GetString(nameHandle);
        return string.Equals(nsValue, "System.Runtime.CompilerServices", StringComparison.Ordinal)
            && string.Equals(typeName, "ReferenceAssemblyAttribute", StringComparison.Ordinal);
    }

    private static string MachineText(Machine machine)
    {
        return machine switch
        {
            Machine.I386 => "I386",
            Machine.Amd64 => "AMD64",
            Machine.Arm64 => "ARM64",
            Machine.Arm => "ARM",
            _ => machine.ToString().ToUpperInvariant()
        };
    }

    private static string PeFormat(PEMagic magic)
    {
        return magic switch
        {
            PEMagic.PE32 => "PE32",
            PEMagic.PE32Plus => "PE32+",
            _ => magic.ToString()
        };
    }
}
