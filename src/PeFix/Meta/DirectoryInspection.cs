namespace PeFix.Meta;

public readonly record struct DirectoryInspection(
    string Directory,
    Inspection[] Results);
