using PeFix.Meta;

namespace PeFix.Cli;

internal static class RefStatText
{
    public static string Token(RefStatus status) => status switch
    {
        RefStatus.Present => "present",
        RefStatus.Missing => "missing",
        RefStatus.VersionConflict => "version_conflict",
        RefStatus.HostProvided => "host_provided",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static RefStatus Highest(IEnumerable<RefEntry> entries)
    {
        RefStatus highest = RefStatus.Present;
        foreach (RefEntry entry in entries)
        {
            if (Rank(entry.Status) > Rank(highest))
                highest = entry.Status;
        }

        return highest;
    }

    private static int Rank(RefStatus status) => status switch
    {
        RefStatus.Present => 0,
        RefStatus.HostProvided => 1,
        RefStatus.VersionConflict => 2,
        RefStatus.Missing => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
