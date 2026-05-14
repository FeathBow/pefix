namespace PeFix.Meta;

public readonly record struct DirInspect(
    string Directory,
    Inspection[] Results);
