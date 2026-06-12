namespace PeFix.Meta;

internal sealed record IfaceSurface(
    HashSet<MemberShape> AbstractShapes,
    HashSet<string> OverrideKeys);
