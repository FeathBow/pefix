using BepInEx;

namespace BepFlag;

[BepInPlugin("test.flag", "Flag Plugin", "1.0.0")]
[BepInDependency("need.flag", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin;
