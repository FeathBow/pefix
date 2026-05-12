namespace PeFix.Meta;

public readonly record struct BepPlugin(
    string Guid,
    string Name,
    string Version,
    BepDep[] Deps);
