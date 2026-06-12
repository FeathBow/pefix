namespace AccessProvider;

public class Api
{
    internal static int Count = 4;

    public static int Open() => 1;

    internal static int Hidden() => 2;
}

internal class Inner
{
    public static int Ping() => 3;
}
