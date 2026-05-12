using System;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInPluginAttribute : Attribute
    {
        public BepInPluginAttribute(string guid, string name, string version)
        {
        }
    }
}

namespace BepMain
{
    [BepInEx.BepInPlugin("com.pefix.main", "Pefix Main", "1.2.3")]
    public sealed class Stub
    {
    }
}
