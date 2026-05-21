namespace PeFix.Patch;

internal static class SnStripBatch
{
    internal static SnBatch Run(string dir, SnStripOpts options)
    {
        string fullDir = Path.GetFullPath(dir);
        var plan = SnStripPlan.ForDir(fullDir, options);
        if (plan.HasRefusals && !options.DryRun)
            return new SnBatch(fullDir, plan.DryResults(), plan.Refusals, ToDryRunDependencies(plan), false);

        if (options.DryRun)
            return new SnBatch(fullDir, plan.DryResults(), plan.Refusals, ToDryRunDependencies(plan), true);

        VerifiedWriteResult[] writes = VerifiedWrite.ApplyBatch(plan.ToRequests(options));
        return plan.BuildBatch(fullDir, writes);
    }

    private static SnDependency[] ToDryRunDependencies(SnStripPlan plan)
    {
        return [.. plan.DependencyTargets.Select(target => new SnDependency(target.Path, null, null, target.Dependency.Ops))];
    }
}
