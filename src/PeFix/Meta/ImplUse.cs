namespace PeFix.Meta;

internal sealed record ImplUse(
    string ClassName,
    IReadOnlyList<TypeSurface> Chain,
    HashSet<MemberShape>? NestedShapes,
    HashSet<string> ExplicitKeys,
    IfaceRef[] Interfaces);
