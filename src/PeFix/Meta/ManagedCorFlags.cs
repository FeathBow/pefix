namespace PeFix.Meta;

public readonly record struct ManagedCorFlags(
    bool IlOnly,
    bool Required32Bit,
    bool Preferred32Bit,
    bool Signed);
