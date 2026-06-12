namespace PeFix.Meta;

public readonly record struct ClosureTree(
    ClosureNode Node,
    ClosureTree[] Children);
