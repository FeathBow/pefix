namespace PeFix.Meta;

public readonly record struct ClosureReport(
    string Directory,
    string[] Entries,
    ClosureChain[] Unresolved,
    ClosureChain[] CycleChains,
    int RefsWalked,
    ProvidedLeafCounts ProvidedLeaves,
    ClosureTree[]? Tree);

public readonly record struct ProvidedLeafCounts(int Total, int Framework);

public readonly record struct ClosureTree(
    ClosureNode Node,
    ClosureTree[] Children);
