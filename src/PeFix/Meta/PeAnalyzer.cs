using System.Buffers.Binary;
using System.Collections.Immutable;
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

        if (IsWebcil(fullPath, fileInfo.Length))
        {
            return Classifier.CreateWebcil(fullPath);
        }

        bool isBundle = IsBundle(fullPath, fileInfo.Length);

        try
        {
            using var mmf = MemoryMappedFile.CreateFromFile(fullPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using MemoryMappedViewStream stream = mmf.CreateViewStream(0, fileInfo.Length, MemoryMappedFileAccess.Read);
            using PEReader peReader = new(stream);
            PeSnapshot snapshot = ReadSnapshot(fullPath, peReader, isBundle);
            return Classifier.Classify(snapshot);
        }
        catch (BadImageFormatException)
        {
            return Classifier.CreateBad(fullPath, "This file is not a valid PE file or is corrupted.");
        }
    }

    private static readonly byte[] WasmMagic = [0x00, 0x61, 0x73, 0x6D];
    private const uint R2rMagic = 0x00525452u;
    private const string ResourceExt = ".resources";

    private static readonly byte[] BundleSig = [
        0x8b, 0x1c, 0xcd, 0x0d, 0xfe, 0xfe, 0xfe, 0xfe,
        0x13, 0x12, 0x13, 0x13, 0x11, 0x06, 0x0b, 0x06
    ];

    private static bool IsWebcil(string path, long length)
    {
        if (length < WasmMagic.Length) return false;
        Span<byte> head = stackalloc byte[WasmMagic.Length];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.ReadExactly(head);
        return head.SequenceEqual(WasmMagic);
    }

    private static bool IsBundle(string path, long length)
    {
        if (length < BundleSig.Length) return false;
        Span<byte> tail = stackalloc byte[BundleSig.Length];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(-BundleSig.Length, SeekOrigin.End);
        fs.ReadExactly(tail);
        return tail.SequenceEqual(BundleSig);
    }

    private static PeSnapshot ReadSnapshot(string path, PEReader peReader, bool isBundle = false)
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
        CliFlags flags = new(
            corFlags.HasFlag(CorFlags.ILOnly),
            corFlags.HasFlag(CorFlags.Requires32Bit),
            corFlags.HasFlag(CorFlags.Prefers32Bit),
            corFlags.HasFlag(CorFlags.StrongNameSigned));
        Signals signals = new(
            flags.Signed || corHeader.StrongNameSignatureDirectory.Size > 0,
            HasPInvoke(reader),
            IsRefAsm(reader),
            !flags.IlOnly);

        string[]? pinvokeDeps = ReadPInvokes(reader);
        string? tfm = ReadTfm(reader);
        string metaVersion = ReadMeta(reader);
        string[]? osPlatforms = ReadOs(reader);
        AsmRef[] assemblyRefs = ReadAsmRefs(reader);
        AsmRef? assemblyDef = ReadAsmDef(reader);
        R2RInfo? r2r = ReadR2R(peReader, corHeader);
        bool isTrimmable = ReadTrim(reader);
        bool moduleNest = HasNest(reader);
        bool moduleRefs = HasRefs(reader);
        bool isSatellite = IsSatellite(reader);

        return new PeSnapshot(
            path, true, true, peFormat, machine, flags, signals,
            pinvokeDeps, tfm, metaVersion, osPlatforms,
            assemblyRefs, assemblyDef, r2r, isTrimmable, moduleNest, moduleRefs,
            isBundle, isSatellite);
    }

    private static bool IsSatellite(MetadataReader reader)
    {
        AssemblyDefinition def = reader.GetAssemblyDefinition();
        string name = reader.GetString(def.Name);
        return name.EndsWith(ResourceExt, StringComparison.OrdinalIgnoreCase);
    }
    private static R2RInfo? ReadR2R(PEReader reader, CorHeader corHeader)
    {
        DirectoryEntry dir = corHeader.ManagedNativeHeaderDirectory;
        if (dir.Size == 0)
            return null;

        PEMemoryBlock block = reader.GetSectionData(dir.RelativeVirtualAddress);
        if (block.Length < 8)
            return null;

        ImmutableArray<byte> content = block.GetContent();
        ReadOnlySpan<byte> data = content.AsSpan();
        uint signature = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (signature != R2rMagic)
            return null;

        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        return new R2RInfo(major, minor);
    }

    private static bool ReadTrim(MetadataReader reader)
    {
        AssemblyDefinition assembly = reader.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!IsAttrMatch(reader, attr, "System.Reflection", "AssemblyMetadataAttribute"))
                continue;

            CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
            if (decoded.FixedArguments.Length < 2)
                continue;

            if (decoded.FixedArguments[0].Value is string key
                && decoded.FixedArguments[1].Value is string value
                && string.Equals(key, "IsTrimmable", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasNest(MetadataReader reader)
    {
        TypeDefinitionHandle moduleHandle = default;
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition td = reader.GetTypeDefinition(handle);
            string ns = reader.GetString(td.Namespace);
            string name = reader.GetString(td.Name);
            if (ns.Length == 0 && string.Equals(name, "<Module>", StringComparison.Ordinal))
            {
                moduleHandle = handle;
                break;
            }
        }

        if (moduleHandle.IsNil)
            return false;

        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition td = reader.GetTypeDefinition(handle);
            TypeDefinitionHandle declaringHandle = td.GetDeclaringType();
            if (!declaringHandle.IsNil && declaringHandle == moduleHandle)
                return true;
        }

        return false;
    }

    private static bool HasRefs(MetadataReader reader)
    {
        foreach (AssemblyFileHandle handle in reader.AssemblyFiles)
        {
            AssemblyFile file = reader.GetAssemblyFile(handle);
            string name = reader.GetString(file.Name);
            if (name.EndsWith(".netmodule", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string[]? ReadPInvokes(MetadataReader reader)
    {
        HashSet<string> modules = new(StringComparer.OrdinalIgnoreCase);

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
            HandleKind.MemberReference => MatchesType(reader, reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent, "System.Runtime.CompilerServices", "ReferenceAssemblyAttribute"),
            HandleKind.MethodDefinition => MatchesType(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(), "System.Runtime.CompilerServices", "ReferenceAssemblyAttribute"),
            _ => false
        };
    }

    private static bool MatchesType(MetadataReader reader, EntityHandle handle, string ns, string name)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeReference:
                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                return MatchesType(reader, typeRef.Namespace, typeRef.Name, ns, name);
            case HandleKind.TypeDefinition:
                TypeDefinition typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return MatchesType(reader, typeDef.Namespace, typeDef.Name, ns, name);
            default:
                return false;
        }
    }

    private static bool MatchesType(MetadataReader reader, StringHandle nsHandle, StringHandle nameHandle, string ns, string name)
    {
        string nsValue = reader.GetString(nsHandle);
        string typeName = reader.GetString(nameHandle);
        return string.Equals(nsValue, ns, StringComparison.Ordinal)
            && string.Equals(typeName, name, StringComparison.Ordinal);
    }

    private static string? ReadTfm(MetadataReader reader)
    {
        AssemblyDefinition assembly = reader.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!IsAttrMatch(reader, attr, "System.Runtime.Versioning", "TargetFrameworkAttribute"))
                continue;

            CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
            if (decoded.FixedArguments.Length > 0 && decoded.FixedArguments[0].Value is string tfm)
            {
                return ParseTfm(tfm);
            }
        }
        return null;
    }

    private static string ParseTfm(string tfmString)
    {
        if (tfmString.StartsWith(".NETCoreApp,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            string ver = tfmString[".NETCoreApp,Version=v".Length..];
            return "net" + ver;
        }
        if (tfmString.StartsWith(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            string ver = tfmString[".NETStandard,Version=v".Length..];
            return "netstandard" + ver;
        }
        if (tfmString.StartsWith(".NETFramework,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            string ver = tfmString[".NETFramework,Version=v".Length..].Replace(".", "");
            return "net" + ver;
        }
        return tfmString;
    }

    private static string ReadMeta(MetadataReader reader)
    {
        return reader.MetadataVersion;
    }

    private static string[]? ReadOs(MetadataReader reader)
    {
        AssemblyDefinition assembly = reader.GetAssemblyDefinition();
        List<string> platforms = [];
        foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!IsAttrMatch(reader, attr, "System.Runtime.Versioning", "SupportedOSPlatformAttribute"))
                continue;

            CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
            if (decoded.FixedArguments.Length > 0 && decoded.FixedArguments[0].Value is string platform)
            {
                platforms.Add(platform);
            }
        }
        return platforms.Count > 0 ? [.. platforms] : null;
    }

    private static bool IsAttrMatch(MetadataReader reader, CustomAttribute attr, string ns, string name)
    {
        return attr.Constructor.Kind switch
        {
            HandleKind.MemberReference => MatchesType(reader, reader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent, ns, name),
            HandleKind.MethodDefinition => MatchesType(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType(), ns, name),
            _ => false
        };
    }

    private static AsmRef? ReadAsmDef(MetadataReader reader)
    {
        AssemblyDefinition def = reader.GetAssemblyDefinition();
        string asmName = reader.GetString(def.Name);
        Version v = def.Version;
        string version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        return new AsmRef(asmName, version);
    }

    private static AsmRef[] ReadAsmRefs(MetadataReader reader)
    {
        List<AsmRef> refs = [];
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference asmRef = reader.GetAssemblyReference(handle);
            string asmName = reader.GetString(asmRef.Name);
            Version v = asmRef.Version;
            string version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            refs.Add(new AsmRef(asmName, version));
        }
        return [.. refs];
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
