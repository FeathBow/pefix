namespace PeFix.Meta;

public readonly record struct ClosureChain(
    ClosureNode Entry,
    ClosureNode[] Segments);
