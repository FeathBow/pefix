namespace PeFix.Patch;

public sealed class UnsafeException : Exception
{
    public UnsafeException()
    {
    }

    public UnsafeException(string message)
        : base(message)
    {
    }

    public UnsafeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
