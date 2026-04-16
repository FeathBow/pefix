namespace PeFix.Meta;

public readonly record struct DupProvider(
    string AsmName,
    string[] Files);
