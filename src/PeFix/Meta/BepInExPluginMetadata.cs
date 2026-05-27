namespace PeFix.Meta;

public readonly record struct BepInExPluginMetadata(
    string Guid,
    string Name,
    string Version,
    BepInExDependencyMetadata[] Deps);
