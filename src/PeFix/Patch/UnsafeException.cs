namespace PeFix.Patch;

public sealed class UnsafeException : Exception
{
    public UnsafeException(string message)
        : base(message)
    {
    }
}
