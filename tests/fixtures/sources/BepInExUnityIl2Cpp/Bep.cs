using System;

// Minimal stand-in for the BepInEx 6 IL2CPP plugin surface. A fixture plugin
// that references this assembly is, statically, a BepInEx 6 / IL2CPP plugin.
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string guid, string name, string version)
        {
            GUID = guid;
            Name = name;
            Version = version;
        }

        public string GUID { get; }

        public string Name { get; }

        public string Version { get; }
    }
}

namespace BepInEx.Unity.IL2CPP
{
    public abstract class BasePlugin;
}
