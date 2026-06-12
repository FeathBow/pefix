using System.Reflection;

namespace ReflCctor;

public static class Stub
{
    static Stub()
    {
        Assembly.Load("CctorMiss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
    }

    public static void Touch()
    {
    }
}
