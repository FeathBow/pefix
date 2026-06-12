namespace PeFix.Meta;

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
