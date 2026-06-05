using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace BepIl2Cpp;

// Deriving from the IL2CPP BasePlugin forces the assembly reference to
// BepInEx.Unity.IL2CPP into the metadata, marking this as a BepInEx 6 / IL2CPP plugin.
[BepInPlugin("test.il2cpp", "Il2Cpp Plugin", "1.0.0")]
public sealed class Plugin : BasePlugin;
