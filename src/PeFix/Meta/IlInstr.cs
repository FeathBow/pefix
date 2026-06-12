namespace PeFix.Meta;

internal readonly record struct IlInstr(
    int OpCode,
    int Operand);
