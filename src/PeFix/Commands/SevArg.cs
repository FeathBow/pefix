using PeFix.Meta;

namespace PeFix.Commands;

internal static class SevArg
{
    private const string Values = "compatible, fixable, cautioned, unsafe, corrupt";

    public static bool TryParse(string value, out Status status)
    {
        switch (value.ToLowerInvariant())
        {
            case "compatible":
                status = Status.Compatible;
                return true;
            case "fixable":
                status = Status.Fixable;
                return true;
            case "cautioned":
                status = Status.Cautioned;
                return true;
            case "unsafe":
                status = Status.Unsafe;
                return true;
            case "corrupt":
                status = Status.Corrupt;
                return true;
            default:
                status = default;
                return false;
        }
    }

    public static CliExit WriteBad(string value)
    {
        return CliErr.Usage($"Unknown severity '{value}'. Expected: {Values}.");
    }
}
