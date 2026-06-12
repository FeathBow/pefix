namespace PeFix.Meta;

internal static class ConfPolicy
{
    public static Confidence For(RefOutcome outcome, bool publishDirProfile) => outcome switch
    {
        RefOutcome.AccessGap => publishDirProfile ? Confidence.Gate : Confidence.Advisory,
        _ => Confidence.Gate
    };

    public static Confidence ForReflection(
        ReflRef reference,
        bool hasCustomResolver,
        bool publishDirProfile)
    {
        return publishDirProfile
            && !hasCustomResolver
            && !reference.AdvisoryOnly
            && !reference.StaticCtor
            ? Confidence.Gate
            : Confidence.Advisory;
    }
}
