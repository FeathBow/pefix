namespace PeFix.Meta;

public readonly record struct MissingRef(string RefName, string NeedVer, string NeedBy);
