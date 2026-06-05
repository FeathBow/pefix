namespace PeFix.Cli;

internal static class IssueCode
{
    public const string AsmConflict = "asm_conflict";
    public const string MissingRef = "missing_ref";
    public const string MissingMember = "missing_member";
    public const string DupProvider = "dup_provider";
    public const string BepMissing = "bep_missing";
    public const string BepCasing = "bep_casing";
    public const string BepDuplicateGuid = "bep_dup_guid";
    public const string BepVersionMismatch = "bep_version_mismatch";
    public const string PluginUnresolvedChain = "plugin_unresolved_chain";
    public const string BepLoaderMismatch = "bep_loader_mismatch";
}
