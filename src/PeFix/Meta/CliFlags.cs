namespace PeFix.Meta;

public readonly record struct CliFlags(
    bool IlOnly,
    bool Required32Bit,
    bool Preferred32Bit,
    bool Signed);
