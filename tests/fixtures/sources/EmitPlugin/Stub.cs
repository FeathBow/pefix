using System;
using System.Reflection.Emit;

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

namespace EmitPlugin
{
    [BepInEx.BepInPlugin("test.emit", "Emit Plugin", "1.0.0")]
    public sealed class Stub
    {
        public object Make()
        {
            return new DynamicMethod("m", typeof(void), Type.EmptyTypes);
        }
    }
}
