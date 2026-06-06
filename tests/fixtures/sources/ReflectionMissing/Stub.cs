using System.Reflection;

namespace ReflectionMissing;

public static class Stub
{
    public static Assembly LoadMissing()
    {
        return Assembly.Load("ReflectionMissingDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
    }
}
