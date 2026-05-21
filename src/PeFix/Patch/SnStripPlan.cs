using PeFix.Meta;

namespace PeFix.Patch;

internal sealed class SnStripPlan
{
    private readonly SelfTarget[] _selfTargets;

    private SnStripPlan(SelfTarget[] selfTargets, SnDependencyTarget[] dependencyTargets, Refusal[] refusals)
    {
        _selfTargets = selfTargets;
        DependencyTargets = dependencyTargets;
        Refusals = refusals;
    }

    internal SnDependencyTarget[] DependencyTargets { get; }
    internal Refusal[] Refusals { get; }
    internal bool HasRefusals => Refusals.Length > 0;

    internal static SnStripPlan ForFile(
        string path,
        byte[] original,
        SnSelfWork self,
        SnStripOpts options)
    {
        SelfTarget target = new(path, original, self);
        DependencyPlan plan = PlanDependencies(new DependencyInput
        {
            Directory = Path.GetDirectoryName(path)!,
            TargetNames = [self.AssemblyName],
            SkipPaths = [path],
            CapturePreflightFailures = true,
            Options = options
        });
        return new SnStripPlan([target], plan.Targets, plan.Refusals);
    }

    internal static SnStripPlan ForDir(string dir, SnStripOpts options)
    {
        SelfTarget[] selfTargets = PlanSelf(dir, options, out Refusal[] refusals);
        if (refusals.Length > 0)
            return new SnStripPlan(selfTargets, [], refusals);

        IReadOnlyList<string> targetNames = [.. selfTargets.Select(target => target.Self.AssemblyName)];
        string[] selfPaths = [.. selfTargets.Select(target => target.Path)];
        DependencyPlan plan = PlanDependencies(new DependencyInput
        {
            Directory = dir,
            TargetNames = targetNames,
            SkipPaths = selfPaths,
            CapturePreflightFailures = false,
            Options = options
        });
        return new SnStripPlan(selfTargets, plan.Targets, plan.Refusals);
    }

    internal SnStripResult[] DryResults()
    {
        return [.. _selfTargets.Select(target => new SnStripResult(
            target.Path,
            null,
            null,
            target.Self.WasSigned,
            SnStripOutcome.DryRun,
            target.Self.HadIvt,
            target.Self.Ops.ToArray(),
            [],
            []))];
    }

    internal VerifiedWrite.Request[] ToRequests(SnStripOpts options)
    {
        return [
            .. _selfTargets.Select(target => SelfRequest(target, options)),
            .. DependencyTargets.Select(target => DependencyRequest(target, options))
        ];
    }

    internal SnBatch BuildBatch(string dir, VerifiedWriteResult[] writes)
    {
        List<SnStripResult> results = [];
        List<SnDependency> dependencies = [];
        int index = 0;
        foreach (SelfTarget target in _selfTargets)
        {
            VerifiedWriteResult write = writes[index++];
            results.Add(new SnStripResult(target.Path, write.BackupPath, write.PlanPath, target.Self.WasSigned, SnStripOutcome.Patched, target.Self.HadIvt, target.Self.Ops.ToArray(), [], []));
        }

        foreach (SnDependencyTarget target in DependencyTargets)
        {
            VerifiedWriteResult write = writes[index++];
            dependencies.Add(new SnDependency(target.Path, write.BackupPath, write.PlanPath, target.Dependency.Ops));
        }

        return new SnBatch(dir, [.. results], [], [.. dependencies], false);
    }

    private static SelfTarget[] PlanSelf(string dir, SnStripOpts options, out Refusal[] refusals)
    {
        var state = new SelfState();
        foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
            AddSelf(dll, options, state);

        refusals = [.. state.Failures];
        return [.. state.Targets];
    }

    private static void AddSelf(
        string path,
        SnStripOpts options,
        SelfState state)
    {
        try
        {
            byte[] original = File.ReadAllBytes(path);
            SnSelfWork self = SnStripWork.AnalyzeSelf(original, options);
            if (!self.WasSigned) return;

            if (!options.DryRun)
                VerifiedWrite.Preflight(path, options.Backup);

            state.Targets.Add(new SelfTarget(path, original, self));
        }
        catch (UnsafeException ex) { state.Failures.Add(new Refusal(path, ex.Message, PeAnalyzer.Inspect(path))); }
        catch (RefusalException ex) { state.Failures.Add(Refusal.Create(path, ex.Message)); }
        catch (BadImageFormatException ex) { state.Failures.Add(Refusal.Create(path, ex.Message)); }
    }

    private static DependencyPlan PlanDependencies(DependencyInput input)
    {
        DependencyState state = new(input.SkipPaths);
        foreach (string dll in Directory.EnumerateFiles(input.Directory, "*.dll"))
            AddDependency(Path.GetFullPath(dll), input, state);

        return new DependencyPlan([.. state.Targets], [.. state.Failures]);
    }

    private static void AddDependency(
        string path,
        DependencyInput input,
        DependencyState state)
    {
        if (state.SkipSet.Contains(path))
            return;

        try
        {
            byte[] original = File.ReadAllBytes(path);
            SnDependencyWork? dependency = SnStripWork.BuildDependency(original, input.TargetNames);
            if (dependency is null) return;

            if (!input.Options.DryRun)
                VerifiedWrite.Preflight(path, input.Options.Backup);

            state.Targets.Add(new SnDependencyTarget
            {
                Path = path,
                Original = original,
                Dependency = dependency.Value,
                TargetNames = input.TargetNames
            });
        }
        catch (BadImageFormatException ex) { state.Failures.Add(Refusal.Create(path, ex.Message)); }
        catch (IOException ex) when (input.CapturePreflightFailures) { state.Failures.Add(Refusal.Create(path, ex.Message)); }
        catch (UnauthorizedAccessException ex) when (input.CapturePreflightFailures) { state.Failures.Add(Refusal.Create(path, ex.Message)); }
    }

    private static VerifiedWrite.Request SelfRequest(SelfTarget target, SnStripOpts options)
    {
        return new VerifiedWrite.Request
        {
            Path = target.Path,
            Original = target.Original,
            Patched = target.Self.Patched,
            Ops = target.Self.Ops,
            Backup = options.Backup,
            Verify = SnVerify.SelfStripped
        };
    }

    private static VerifiedWrite.Request DependencyRequest(SnDependencyTarget target, SnStripOpts options)
    {
        return new VerifiedWrite.Request
        {
            Path = target.Path,
            Original = target.Original,
            Patched = target.Dependency.Patched,
            Ops = target.Dependency.Ops,
            Backup = options.Backup,
            Verify = tmpPath => SnVerify.DepTokensCleared(tmpPath, target.TargetNames)
        };
    }

    private readonly record struct SelfTarget(
        string Path,
        byte[] Original,
        SnSelfWork Self);

    private readonly record struct DependencyPlan(
        SnDependencyTarget[] Targets,
        Refusal[] Refusals);

    private sealed class SelfState
    {
        public List<SelfTarget> Targets { get; } = [];
        public List<Refusal> Failures { get; } = [];
    }

    private sealed class DependencyInput
    {
        public required string Directory { get; init; }
        public required IReadOnlyList<string> TargetNames { get; init; }
        public required IReadOnlyList<string> SkipPaths { get; init; }
        public required bool CapturePreflightFailures { get; init; }
        public required SnStripOpts Options { get; init; }
    }

    private sealed class DependencyState
    {
        public DependencyState(IReadOnlyList<string> skipPaths)
        {
            SkipSet = new HashSet<string>(skipPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        }

        public HashSet<string> SkipSet { get; }
        public List<SnDependencyTarget> Targets { get; } = [];
        public List<Refusal> Failures { get; } = [];
    }
}
