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
        return result.Status switch
        {
            Status.Compatible => "This assembly is already portable across platforms.",
            Status.Fixable => "This assembly uses a platform-specific header, but the managed code is portable and can be fixed.",
            Status.Cautioned when result.Signals.StrongName => "This assembly can be fixed, but the strong name signature will be invalidated.",
            Status.Cautioned => "This assembly can be fixed, but native dependencies may still fail on the target platform.",
            Status.Unsafe => result.PrimaryCause,
            Status.Corrupt => result.PrimaryCause,
            _ => "The compatibility status could not be determined."
        };
    }
}
