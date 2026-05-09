namespace PeFix.Cli;

internal sealed record DirMiss(
    string Assembly,
    string Version,
    string RequiredBy);
