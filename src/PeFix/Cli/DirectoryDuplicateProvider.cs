namespace PeFix.Cli;

internal sealed record DirectoryDuplicateProvider(
    string Assembly,
    string[] Files);
