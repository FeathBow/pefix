namespace PeFix.Cli;

internal sealed record DirectoryMissingReference(
    string Assembly,
    string Version,
    string RequiredBy);
