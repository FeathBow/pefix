namespace PeFix.Meta;

internal sealed class PeView(
    MethodRefUse[] methodRefs,
    FieldRefUse[] fieldRefs,
    MemSurface memberSurface,
    ImplUse[] implUses,
    AccessInfo accessInfo,
    ReflScan reflection)
{
    public MethodRefUse[] MethodRefs { get; } = methodRefs;
    public FieldRefUse[] FieldRefs { get; } = fieldRefs;
    public MemSurface MemSurface { get; } = memberSurface;
    public ImplUse[] ImplUses { get; } = implUses;
    public AccessInfo AccessInfo { get; } = accessInfo;
    public ReflScan Reflection { get; } = reflection;
}
