namespace PeFix.Cli;

internal sealed record DirectoryConflict(
    string Assembly,
    string Expected,
    string Actual,
    string ReferencedBy,
    string ProvidedBy);
