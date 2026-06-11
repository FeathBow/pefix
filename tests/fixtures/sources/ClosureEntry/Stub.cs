using System;
using ClosureMid;

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

namespace ClosureEntry
{
    [BepInEx.BepInPlugin("test.closure", "Closure Plugin", "1.0.0")]
    public sealed class Stub
    {
        public ClosureMid.Stub? MidRef { get; set; }
    }
}
