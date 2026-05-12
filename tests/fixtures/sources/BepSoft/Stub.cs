using BepInEx;

namespace BepSoft;

[BepInPlugin("test.soft", "Soft Plugin", "1.0.0")]
[BepInDependency("need.soft", VersionRange = ">=1.0.0", Flags = BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin;
