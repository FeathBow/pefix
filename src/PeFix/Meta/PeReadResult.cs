namespace PeFix.Meta;

internal readonly record struct PeReadResult(
    PeSnapshot Snapshot,
    PeView? View);
