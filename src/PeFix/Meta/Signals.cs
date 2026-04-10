namespace PeFix.Meta;

public readonly record struct Signals(
    bool StrongName,
    bool HasPInvoke,
    bool IsRefAsm,
    bool IsMixedMode);
