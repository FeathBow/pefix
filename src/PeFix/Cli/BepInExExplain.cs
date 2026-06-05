using PeFix.Meta;
using System.Collections.ObjectModel;

namespace PeFix.Cli;

internal static class BepInExExplain
{
    public static BepInExExplainResult Explain(
        Inspection[] results,
        PathRelativizer rel,
        BepInExProviderIndex providerIndex)
    {
        return Explain(results, rel, providerIndex, null, null);
    }

    public static BepInExExplainResult Explain(
        Inspection[] results,
        PathRelativizer rel,
        BepInExProviderIndex providerIndex,
        ClosureReport? closure,
        LoaderTarget? declaredHost = null,
        IReadOnlyDictionary<string, LoaderTarget>? loaderByPath = null)
    {
        List<DirectoryIssue> reportIssues = [];
        Dictionary<string, string> fileStates = new(StringComparer.Ordinal);
        loaderByPath ??= LoaderTargetReader.FromInspections(results);
        PluginLookup pluginLookup = BuildPluginLookup(results);
        ExplainCtx ctx = new(
            reportIssues,
            fileStates,
            rel,
            providerIndex,
            pluginLookup.HasPlugins,
            pluginLookup.ByAssembly);
        AddDuplicateGuidIssues(ctx);
        foreach (Inspection result in results)
            AddResult(ctx, result);

        MismatchResult loaderMismatch = LoaderMismatchExplain.Explain(
            new MismatchCtx(rel, results, declaredHost, loaderByPath));
        foreach (string path in loaderMismatch.BlockedPaths)
            fileStates[path] = BepStateCode.LoaderMismatch;
        reportIssues.AddRange(loaderMismatch.Issues);

        if (closure.HasValue)
            AddClosureIssues(ctx, closure.Value);

        return new BepInExExplainResult([.. reportIssues], new ReadOnlyDictionary<string, string>(fileStates));
    }

    private static void AddResult(ExplainCtx ctx, Inspection result)
    {
        if (!result.BepInEx.HasValue)
        {
            if (ctx.HasPlugins)
                ctx.FileStates[result.Path] = StateForNonPlugin(result);

            return;
        }

        ctx.FileStates[result.Path] = BepStateCode.Plugin;
        foreach (BepInExPluginMetadata plugin in result.BepInEx.Value.Plugins)
            AddPlugin(ctx, result, plugin);
    }

    private static void AddPlugin(ExplainCtx ctx, Inspection result, BepInExPluginMetadata plugin)
    {
        foreach (BepInExDependencyMetadata dependency in plugin.Deps)
        {
            if (!dependency.Hard)
                continue;

            BepInExProviderMatch match = ctx.Providers.MatchFor(dependency.Guid);
            if (match is BepInExProviderMatch.Exact)
            {
                AddVersionMismatch(ctx, result, plugin, dependency);
                continue;
            }

            ctx.FileStates[result.Path] = StateFor(match);
            BepInExPluginProvider[] providers = match is BepInExProviderMatch.CaseOnly
                ? ctx.Providers.ForAnyCase(dependency.Guid)
                : [];
            ctx.Issues.Add(RepairGuide.ForIssue(
                IssueCodeFor(match),
                dependency.Guid,
                Summary(plugin.Guid, dependency.Guid, match),
                [ctx.Rel.RelativePath(result.Path)],
                IssueEvidence.ForBepDependency(
                    dependency.Range,
                    providerFiles: providers.Length == 0 ? null : ProviderFiles(ctx, providers))));
        }
    }

    private static void AddDuplicateGuidIssues(ExplainCtx ctx)
    {
        foreach (IGrouping<string, BepInExPluginProvider> group in ctx.Providers
                     .DuplicateProviders()
                     .GroupBy(provider => provider.Guid, StringComparer.Ordinal))
        {
            BepInExPluginProvider[] providers = [.. group];
            string[] providerFiles = [.. providers.Select(provider => ctx.Rel.RelativePath(provider.File))];
            ctx.Issues.Add(RepairGuide.ForIssue(
                IssueCode.BepDuplicateGuid,
                group.Key,
                $"Multiple BepInEx plugins declare GUID {group.Key}: {string.Join(", ", providerFiles)}.",
                providerFiles,
                IssueEvidence.ForProviderFiles(providerFiles)));
        }
    }

    private static void AddVersionMismatch(
        ExplainCtx ctx,
        Inspection result,
        BepInExPluginMetadata plugin,
        BepInExDependencyMetadata dependency)
    {
        BepInExPluginProvider[] providers = ctx.Providers.ForExactGuid(dependency.Guid);
        if (providers.Length != 1 || BepVersionRange.IsMinimumSatisfied(dependency.Range, providers[0].Version))
            return;

        string[] providerFiles = ProviderFiles(ctx, providers);
        ctx.FileStates[result.Path] = BepStateCode.VersionMismatch;
        ctx.Issues.Add(RepairGuide.ForIssue(
            IssueCode.BepVersionMismatch,
            dependency.Guid,
            $"{plugin.Guid} requires BepInEx plugin {dependency.Guid} {dependency.Range}, but {providers[0].Version} is provided by {providerFiles[0]}.",
            [ctx.Rel.RelativePath(result.Path), providerFiles[0]],
            IssueEvidence.ForBepDependency(
                dependency.Range,
                providers[0].Version,
                providerFiles)));
    }

    private static void AddClosureIssues(ExplainCtx ctx, ClosureReport closure)
    {
        foreach (ClosureChain chain in closure.Unresolved)
        {
            if (!ctx.ByAssembly.TryGetValue(chain.Entry.AssemblyName, out string? file))
                continue;

            ctx.FileStates[file] = BepStateCode.UnresolvedChain;
            ctx.Issues.Add(RepairGuide.ForIssue(
                IssueCode.PluginUnresolvedChain,
                chain.Segments[^1].AssemblyName,
                ChainSummary(chain),
                [ctx.Rel.RelativePath(file)],
                IssueEvidence.ForClosure(
                    ctx.Rel.RelativePath(file),
                    [.. chain.Segments.Select(segment => $"{segment.AssemblyName}.dll")],
                    $"{chain.Segments[^1].AssemblyName}.dll")));
        }
    }

    private static PluginLookup BuildPluginLookup(Inspection[] results)
    {
        Dictionary<string, string> byAssembly = new(StringComparer.Ordinal);
        bool hasPlugins = false;
        foreach (Inspection result in results)
        {
            if (result.BepInEx is not { Plugins.Length: > 0 })
                continue;

            hasPlugins = true;
            if (result.AssemblyDefinition is { } assembly)
                byAssembly.TryAdd(assembly.Name, result.Path);
        }

        return new PluginLookup(hasPlugins, byAssembly);
    }

    private static string StateForNonPlugin(Inspection result)
    {
        return result.ValidPe && result.HasCliHeader && result.Status is not Status.Unsafe and not Status.Corrupt
            ? BepStateCode.Helper
            : BepStateCode.Invalid;
    }

    private static string[] ProviderFiles(ExplainCtx ctx, BepInExPluginProvider[] providers)
    {
        return [.. providers.Select(provider => ctx.Rel.RelativePath(provider.File))];
    }

    private static string ChainSummary(ClosureChain chain)
    {
        string current = chain.Entry.AssemblyName + ".dll";
        List<string> parts = [];
        foreach (ClosureNode segment in chain.Segments)
        {
            string next = segment.AssemblyName + ".dll";
            string verb = segment.Kind is ChainKind.Unresolved ? "needs" : "loads";
            parts.Add($"{current} {verb} {next}");
            current = next;
        }

        return string.Join("; ", parts) + ".";
    }

    private static string StateFor(BepInExProviderMatch match)
    {
        return match is BepInExProviderMatch.CaseOnly
            ? BepStateCode.GuidCaseMismatch
            : BepStateCode.MissingDependency;
    }

    private static string IssueCodeFor(BepInExProviderMatch match)
    {
        return match is BepInExProviderMatch.CaseOnly
            ? IssueCode.BepCasing
            : IssueCode.BepMissing;
    }

    private static string Summary(
        string plugin,
        string dependency,
        BepInExProviderMatch match)
    {
        string reason = match is BepInExProviderMatch.CaseOnly
            ? "only a case-different plugin GUID was found"
            : "no matching plugin GUID was found";
        return $"{plugin} requires BepInEx plugin {dependency}, but {reason}.";
    }

    private readonly record struct PluginLookup(
        bool HasPlugins,
        IReadOnlyDictionary<string, string> ByAssembly);

    private readonly record struct ExplainCtx(
        List<DirectoryIssue> Issues,
        Dictionary<string, string> FileStates,
        PathRelativizer Rel,
        BepInExProviderIndex Providers,
        bool HasPlugins,
        IReadOnlyDictionary<string, string> ByAssembly);
}
