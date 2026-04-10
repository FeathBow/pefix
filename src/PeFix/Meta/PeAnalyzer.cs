using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

public static class PeAnalyzer
{
    public static Inspection Inspect(string path)
    {
        var fullPath = Path.GetFullPath(path);
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
            using var stream = mmf.CreateViewStream(0, fileInfo.Length, MemoryMappedFileAccess.Read);
            using var peReader = new PEReader(stream);
            var snapshot = ReadSnapshot(fullPath, peReader);
            return Classifier.Classify(snapshot);
        }
        catch (BadImageFormatException)
        {
            return Classifier.CreateBad(fullPath, "This file is not a valid PE file or is corrupted.");
        }
    }

    private static PeSnapshot ReadSnapshot(string path, PEReader peReader)
    {
        var headers = peReader.PEHeaders;
        var peHeader = headers.PEHeader;
        if (peHeader is null)
        {
            return new PeSnapshot(path, false, false, null, null, default, default);
        }

        var peFormat = PeFormat(peHeader.Magic);
        var machine = MachineText(headers.CoffHeader.Machine);
        var corHeader = headers.CorHeader;
        if (corHeader is null)
        {
            return new PeSnapshot(path, true, false, peFormat, machine, default, default);
        }

        var corFlags = corHeader.Flags;
        var reader = peReader.GetMetadataReader();
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

        return new PeSnapshot(path, true, true, peFormat, machine, flags, signals);
    }

    private static bool HasPInvoke(MetadataReader reader)
    {
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRefAsm(MetadataReader reader)
    {
        var assembly = reader.GetAssemblyDefinition();
        foreach (var handle in assembly.GetCustomAttributes())
        {
            if (IsRefAsm(reader, reader.GetCustomAttribute(handle)))
            {
                return true;
            }
        }

        return false;
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
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                return MatchesType(reader, typeRef.Namespace, typeRef.Name);
            case HandleKind.TypeDefinition:
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return MatchesType(reader, typeDef.Namespace, typeDef.Name);
            default:
                return false;
        }
    }

    private static bool MatchesType(MetadataReader reader, StringHandle nsHandle, StringHandle nameHandle)
    {
        var nsValue = reader.GetString(nsHandle);
        var typeName = reader.GetString(nameHandle);
        return nsValue == "System.Runtime.CompilerServices" && typeName == "ReferenceAssemblyAttribute";
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
