using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AccessConsumer")]
[assembly: InternalsVisibleTo("AccessSkipper")]

namespace AccessProvider;

public class Api
{
    internal static int Count;

    public static int Open() => 1;

    internal static int Hidden() => 2;
}

internal class Inner
{
    public static int Ping() => 3;
}
