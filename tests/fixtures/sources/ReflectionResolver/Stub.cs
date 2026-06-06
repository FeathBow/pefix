using System.Reflection;

namespace ReflectionResolver;

public static class Stub
{
    public static Assembly LoadWithResolver()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        return Assembly.Load("ReflectionResolverMissing, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        return null;
    }
}
