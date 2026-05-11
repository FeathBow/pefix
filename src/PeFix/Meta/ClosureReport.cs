namespace PeFix.Meta;

public readonly record struct ClosureReport(
    string Directory,
    string[] Entries,
    ClosureChain[] Unresolved,
    ClosureChain[] CycleChains,
    int RefsWalked,
    int HostLeaves);

public readonly record struct ClosureChain(
    ClosureNode Entry,
    ClosureNode[] Segments);
