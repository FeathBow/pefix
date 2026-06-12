using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

internal readonly record struct PeSnapshot(
    string Path,
    bool ValidPe,
    bool HasCliHeader,
    string? PeFormat,
    string? Machine,
    ManagedCorFlags ManagedCorFlags,
    Signals Signals,
    string[]? PInvokeDeps = null,
    string? Tfm = null,
    string? MetaVersion = null,
    string[]? OsPlatforms = null,
    AssemblyIdentity[]? AssemblyReferences = null,
    AssemblyIdentity? AssemblyDefinition = null,
    ReadyToRunInfo? ReadyToRun = null,
    bool IsTrimmable = false,
    bool HasNest = false,
    bool HasRefs = false,
    bool IsBundle = false,
    bool IsSatellite = false,
    BepInExMetadata? BepInEx = null);

internal readonly record struct PeRead(
    string Path,
    PEReader Reader,
    MetadataReader Metadata);

internal readonly record struct PeReadResult(
    PeSnapshot Snapshot,
    PeView? View);

internal sealed class PeView(
    MethodRefUse[] methodRefs,
    FieldRefUse[] fieldRefs,
    MemSurface memberSurface,
    ReflScan reflection)
{
    public MethodRefUse[] MethodRefs { get; } = methodRefs;
    public FieldRefUse[] FieldRefs { get; } = fieldRefs;
    public MemSurface MemSurface { get; } = memberSurface;
    public ReflScan Reflection { get; } = reflection;
}

internal readonly record struct MethodRefUse(
    string AssemblyName,
    string TypeName,
    string MemberName,
    int ParameterCount);

internal readonly record struct FieldRefUse(
    string AssemblyName,
    string TypeName,
    string FieldName);

internal readonly record struct TypeRefUse(
    string AssemblyName,
    string TypeName);

internal readonly record struct MemberShape(string Name, int ParameterCount);

internal sealed class MemSurface(
    HashSet<string> typeNames,
    Dictionary<string, HashSet<MemberShape>> membersByType,
    Dictionary<string, HashSet<string>> fieldsByType)
{
    public bool ContainsType(string typeName)
    {
        return typeNames.Contains(typeName);
    }

    public bool TryGetMembers(string typeName, out HashSet<MemberShape> members)
    {
        return membersByType.TryGetValue(typeName, out members!);
    }

    public bool TryGetFields(string typeName, out HashSet<string> fields)
    {
        return fieldsByType.TryGetValue(typeName, out fields!);
    }
}
