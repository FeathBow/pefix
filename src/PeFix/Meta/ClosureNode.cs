namespace PeFix.Meta;

public readonly record struct ClosureNode(
    string AssemblyName,
    string Version,
    ChainKind Kind);
