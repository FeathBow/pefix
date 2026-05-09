namespace PeFix.Cli;

internal sealed record DirConf(
    string Assembly,
    string Expected,
    string Actual,
    string ReferencedBy,
    string ProvidedBy);
