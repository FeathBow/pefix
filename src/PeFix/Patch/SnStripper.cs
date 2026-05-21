namespace PeFix.Patch;

public static class SnStripper
{
    public static SnStripResult Strip(string path, SnStripOpts options)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] origBytes = File.ReadAllBytes(fullPath);
        SnSelfWork self = SnStripWork.AnalyzeSelf(origBytes, options);

        if (!self.WasSigned)
            return new SnStripResult(fullPath, null, null, false, SnStripOutcome.Unsigned, self.HadIvt, [], [], []);

        var plan = SnStripPlan.ForFile(fullPath, origBytes, self, options);
        var context = new SnStripContext
        {
            Path = fullPath,
            Self = self,
            DependencyTargets = plan.DependencyTargets,
            Refusals = plan.Refusals
        };

        if (plan.HasRefusals && !options.DryRun)
            return new SnStripResult(fullPath, null, null, self.WasSigned, SnStripOutcome.DepRefused, self.HadIvt, self.Ops.ToArray(), [], plan.Refusals);

        if (options.DryRun)
            return BuildDryRun(context);

        VerifiedWriteResult[] writes = VerifiedWrite.ApplyBatch(plan.ToRequests(options));
        return BuildResult(context, writes);
    }

    public static SnBatch StripDir(string dir, SnStripOpts options)
    {
        return SnStripBatch.Run(dir, options);
    }

    private static SnStripResult BuildResult(SnStripContext context, VerifiedWriteResult[] writes)
    {
        VerifiedWriteResult selfWrite = writes[0];
        List<SnDependency> dependencies = [];
        for (int index = 0; index < context.DependencyTargets.Length; index++)
        {
            SnDependencyTarget target = context.DependencyTargets[index];
            VerifiedWriteResult write = writes[index + 1];
            dependencies.Add(new SnDependency(target.Path, write.BackupPath, write.PlanPath, target.Dependency.Ops));
        }

        return new SnStripResult(
            context.Path,
            selfWrite.BackupPath,
            selfWrite.PlanPath,
            context.Self.WasSigned,
            SnStripOutcome.Patched,
            context.Self.HadIvt,
            context.Self.Ops.ToArray(),
            [.. dependencies],
            []);
    }

    private static SnStripResult BuildDryRun(SnStripContext context)
    {
        SnDependency[] dependencies = [.. context.DependencyTargets.Select(target => new SnDependency(target.Path, null, null, target.Dependency.Ops))];
        return new SnStripResult(
            context.Path,
            null,
            null,
            context.Self.WasSigned,
            SnStripOutcome.DryRun,
            context.Self.HadIvt,
            context.Self.Ops.ToArray(),
            dependencies,
            context.Refusals);
    }

    private sealed class SnStripContext
    {
        public required string Path { get; init; }
        public required SnSelfWork Self { get; init; }
        public required SnDependencyTarget[] DependencyTargets { get; init; }
        public required Refusal[] Refusals { get; init; }
    }
}
