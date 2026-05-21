namespace PeFix.Patch;

public sealed class RefusalException : Exception
{
    public RefusalException()
    {
    }

    public RefusalException(string message)
        : base(message)
    {
    }

    public RefusalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
