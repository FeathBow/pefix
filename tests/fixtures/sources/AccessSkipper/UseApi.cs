using System;
using System.Runtime.CompilerServices;

[assembly: IgnoresAccessChecksTo("AccessProvider")]

namespace AccessSkipper
{
    public sealed class UseApi
    {
        public int Run()
        {
            return AccessProvider.Api.Hidden() + AccessProvider.Inner.Ping();
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
