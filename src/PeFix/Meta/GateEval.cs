namespace PeFix.Meta;

internal static class GateEval
{
    public static bool Meets(Status status, Status threshold) => threshold switch
    {
        Status.Compatible => true,
        Status.Fixable => status is Status.Fixable or Status.Cautioned or Status.Unsafe or Status.Corrupt,
        Status.Cautioned => status is Status.Cautioned or Status.Unsafe or Status.Corrupt,
        Status.Unsafe => status is Status.Unsafe or Status.Corrupt,
        Status.Corrupt => status == Status.Corrupt,
        _ => throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Unsupported gate threshold.")
    };
}
