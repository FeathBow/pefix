namespace PeFix.Cli;

internal static class BepInExExplainState
{
    public const string Plugin = "plugin";
    public const string HelperLibrary = "helper_library";
    public const string InvalidArtifact = "invalid_artifact";
    public const string BlockedMissingDependency = "blocked_missing_bep_dependency";
    public const string BlockedGuidCaseMismatch = "blocked_guid_case_mismatch";
    public const string BlockedVersionMismatch = "blocked_bep_version_mismatch";
    public const string RiskUnresolvedAssemblyChain = "risk_unresolved_assembly_chain";
}
