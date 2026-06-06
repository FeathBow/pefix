using System.Reflection;

namespace ReflectionPresent;

public static class Stub
{
    public static Assembly LoadPresent()
    {
        return Assembly.Load("ReflectionTarget, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
    }
}
