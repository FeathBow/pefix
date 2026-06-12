namespace PeFix.Meta;

internal readonly record struct DecodeResult(
    IlInstr[] Instructions,
    bool Desynced);
