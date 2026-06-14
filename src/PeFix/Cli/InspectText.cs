using PeFix.Meta;

namespace PeFix.Cli;

internal static class InspectText
{
    public static string Action(Inspection result)
    {
        return result.NextSteps.Length > 0 ? result.NextSteps[0] : "No action available.";
    }

    public static string Summary(Inspection result)
    {
        // Status lumps many ReasonCodes (Cautioned = native/r2r/trimmable/bundle/...), so
        // the precise per-reason cause is PrimaryCause, not a status-keyed string.
        return result.Status switch
        {
            Status.Compatible => "This assembly is already portable across platforms.",
            Status.Fixable => "This assembly uses a platform-specific header, but the managed code is portable and can be fixed.",
            _ => result.PrimaryCause
        };
    }
}
