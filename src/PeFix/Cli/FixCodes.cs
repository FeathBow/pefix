namespace PeFix.Cli;

internal static class FixResult
{
    public const string DryRun = "dry_run";
    public const string Patched = "patched";
    public const string Unchanged = "unchanged";
}

internal static class FixVerify
{
    public const string Ok = "ok";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
}
