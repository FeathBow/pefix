namespace PeFix.Meta;

public readonly record struct ClosureReport(
    string Directory,
    string[] Entries,
    ClosureChain[] Unresolved,
    ClosureChain[] CycleChains,
    int RefsWalked,
    int HostLeaves);
