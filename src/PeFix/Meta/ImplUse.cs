namespace PeFix.Meta;

internal sealed record ImplUse(
    string ClassName,
    HashSet<MemberShape> ClassShapes,
    HashSet<string> ExplicitKeys,
    IfaceRef[] Interfaces);
