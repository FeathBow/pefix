namespace PeFix.Cli;

internal static class BepStateCode
{
    public const string Plugin = "plugin";
    public const string Helper = "helper_library";
    public const string Invalid = "invalid_artifact";
    public const string MissingDependency = "blocked_missing_bep_dependency";
    public const string GuidCaseMismatch = "blocked_guid_case_mismatch";
    public const string VersionMismatch = "blocked_bep_version_mismatch";
    public const string UnresolvedChain = "risk_unresolved_assembly_chain";
    public const string LoaderMismatch = "blocked_loader_mismatch";
}
