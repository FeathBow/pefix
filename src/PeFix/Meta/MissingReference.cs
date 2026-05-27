namespace PeFix.Meta;

public readonly record struct MissingReference(string ReferenceName, string RequiredVersion, string RequiredBy);
