namespace PeFix.Meta;

internal readonly record struct ClosureNode(
    string AssemblyName,
    string Version,
    ChainKind Kind);

internal enum ChainKind { Entry, Resolved, Framework, Unresolved, Cycle }
