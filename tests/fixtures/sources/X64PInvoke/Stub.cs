using System.Runtime.InteropServices;

namespace X64PInvoke;

internal static class Stub
{
    [DllImport("native")]
    public static extern int Invoke();
}
