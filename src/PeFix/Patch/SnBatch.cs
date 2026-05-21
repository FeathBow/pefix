namespace PeFix.Patch;

public readonly record struct SnBatch(
    string Directory,
    SnStripResult[] Results,
    Refusal[] Refusals,
    SnDependency[] Deps,
    bool DryRun)
{
    public string Outcome
    {
        get
        {
            if (DryRun)
                return "dry_run";

            if (Refusals.Length > 0)
                return "refused";

            if (Results.Any(result => result.WasPatched) || Deps.Length > 0)
                return "patched";

            return "unchanged";
        }
    }
}
