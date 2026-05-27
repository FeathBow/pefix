using PeFix.Meta;
using System.Collections.ObjectModel;

namespace PeFix.Cli;

internal static class BepInExExplain
{
    public static BepInExExplainResult Explain(
        Inspection[] results,
        ScanPathRelativizer rel,
        BepInExProviderIndex index)
    {
        return Explain(results, rel, index, null);
    }

    public static BepInExExplainResult Explain(
        Inspection[] results,
        ScanPathRelativizer rel,
        BepInExProviderIndex index,
        ClosureReport? closure)
    {
        List<DirectoryIssue> issues = [];
        Dictionary<string, string> states = new(StringComparer.Ordinal);
        bool hasBepInExContext = results.Any(result => result.BepInEx is { Plugins.Length: > 0 });
        ExplainContext context = new(issues, states, rel, index, hasBepInExContext, results);
        AddDuplicateGuidIssues(context);
        foreach (Inspection result in results)
            AddResult(context, result);

        if (closure.HasValue)
            AddClosureIssues(context, closure.Value);

        return new BepInExExplainResult([.. issues], new ReadOnlyDictionary<string, string>(states));
    }

    private static void AddResult(ExplainContext context, Inspection result)
    {
        if (!result.BepInEx.HasValue)
        {
            if (context.HasBepInExContext)
                context.States[result.Path] = StateForNonPlugin(result);

            return;
        }

        context.States[result.Path] = BepInExExplainState.Plugin;
        foreach (BepInExPluginMetadata plugin in result.BepInEx.Value.Plugins)
            AddPlugin(context, result, plugin);
    }

    private static void AddPlugin(ExplainContext context, Inspection result, BepInExPluginMetadata plugin)
    {
        foreach (BepInExDependencyMetadata dependency in plugin.Deps)
        {
            if (!dependency.Hard)
                continue;

            BepInExDependencyProviderPresence presence = context.Index.ProviderPresenceFor(dependency.Guid);
            if (presence is BepInExDependencyProviderPresence.ExactProviderFound)
            {
                AddVersionMismatch(context, result, plugin, dependency);
                continue;
            }

            context.States[result.Path] = StateFor(presence);
            BepInExPluginProvider[] providers = presence is BepInExDependencyProviderPresence.CaseMismatchProviderFound
                ? context.Index.ProvidersForCaseInsensitiveGuid(dependency.Guid)
                : [];
            context.Issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCodeFor(presence),
                Subject = dependency.Guid,
                Summary = Summary(plugin.Guid, dependency.Guid, presence),
                Files = [context.Rel.RelativePath(result.Path)],
                Evidence = new IssueEvidence(
                    DeclaredRange: dependency.Range,
                    ProviderFiles: RelProviderFiles(context, providers))
            }));
        }
    }

    private static void AddDuplicateGuidIssues(ExplainContext context)
    {
        foreach (IGrouping<string, BepInExPluginProvider> group in context.Index
                     .DuplicateProviders()
                     .GroupBy(provider => provider.Guid, StringComparer.Ordinal))
        {
            BepInExPluginProvider[] providers = [.. group];
            context.Issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.BepDuplicateGuid,
                Subject = group.Key,
                Summary = $"Multiple BepInEx plugins declare GUID {group.Key}: {string.Join(", ", providers.Select(provider => context.Rel.RelativePath(provider.File)))}.",
                Files = [.. providers.Select(provider => context.Rel.RelativePath(provider.File))],
                Evidence = new IssueEvidence(
                    ProviderFiles: [.. providers.Select(provider => context.Rel.RelativePath(provider.File))])
            }));
        }
    }

    private static void AddVersionMismatch(
        ExplainContext context,
        Inspection result,
        BepInExPluginMetadata plugin,
        BepInExDependencyMetadata dependency)
    {
        BepInExPluginProvider[] providers = context.Index.ProvidersForExactGuid(dependency.Guid);
        if (providers.Length != 1 || BepVersionRange.IsMinimumSatisfied(dependency.Range, providers[0].Version))
            return;

        string[] providerFiles = ProviderFiles(context, providers);
        context.States[result.Path] = BepInExExplainState.BlockedVersionMismatch;
        context.Issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
        {
            Code = IssueCode.BepVersionMismatch,
            Subject = dependency.Guid,
            Summary = $"{plugin.Guid} requires BepInEx plugin {dependency.Guid} {dependency.Range}, but {providers[0].Version} is provided by {providerFiles[0]}.",
            Files = [context.Rel.RelativePath(result.Path), providerFiles[0]],
            Evidence = new IssueEvidence(
                DeclaredRange: dependency.Range,
                PresentVersion: providers[0].Version,
                ProviderFiles: providerFiles)
        }));
    }

    private static void AddClosureIssues(ExplainContext context, ClosureReport closure)
    {
        foreach (ClosureChain chain in closure.Unresolved)
        {
            if (PluginFileFor(context, chain.Entry.AssemblyName) is not { } file)
                continue;

            context.States[file] = BepInExExplainState.RiskUnresolvedAssemblyChain;
            context.Issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
            {
                Code = IssueCode.PluginUnresolvedChain,
                Subject = chain.Segments[^1].AssemblyName,
                Summary = ChainSummary(chain),
                Files = [context.Rel.RelativePath(file)],
                Evidence = new IssueEvidence(
                    EntryFile: context.Rel.RelativePath(file),
                    RequestChain: [.. chain.Segments.Select(segment => $"{segment.AssemblyName}.dll")],
                    MissingLeaf: $"{chain.Segments[^1].AssemblyName}.dll")
            }));
        }
    }

    private static string? PluginFileFor(ExplainContext context, string assemblyName)
    {
        foreach (Inspection result in context.Results)
        {
            if (result.AssemblyDefinition?.Name != assemblyName || !result.BepInEx.HasValue)
                continue;

            if (result.BepInEx.Value.Plugins.Length > 0)
                return result.Path;
        }

        return null;
    }

    private static string StateForNonPlugin(Inspection result)
    {
        return result.ValidPe && result.HasCliHeader && result.Status is not Status.Unsafe and not Status.Corrupt
            ? BepInExExplainState.HelperLibrary
            : BepInExExplainState.InvalidArtifact;
    }

    private static string[]? RelProviderFiles(ExplainContext context, BepInExPluginProvider[] providers)
    {
        return providers.Length == 0
            ? null
            : ProviderFiles(context, providers);
    }

    private static string[] ProviderFiles(ExplainContext context, BepInExPluginProvider[] providers)
    {
        return [.. providers.Select(provider => context.Rel.RelativePath(provider.File))];
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

    private static string StateFor(BepInExDependencyProviderPresence presence)
    {
        return presence is BepInExDependencyProviderPresence.CaseMismatchProviderFound
            ? BepInExExplainState.BlockedGuidCaseMismatch
            : BepInExExplainState.BlockedMissingDependency;
    }

    private static string IssueCodeFor(BepInExDependencyProviderPresence presence)
    {
        return presence is BepInExDependencyProviderPresence.CaseMismatchProviderFound
            ? IssueCode.BepCasing
            : IssueCode.BepMissing;
    }

    private static string Summary(
        string plugin,
        string dependency,
        BepInExDependencyProviderPresence presence)
    {
        string reason = presence is BepInExDependencyProviderPresence.CaseMismatchProviderFound
            ? "only a case-different plugin GUID was found"
            : "no matching plugin GUID was found";
        return $"{plugin} requires BepInEx plugin {dependency}, but {reason}.";
    }

    private readonly record struct ExplainContext(
        List<DirectoryIssue> Issues,
        Dictionary<string, string> States,
        ScanPathRelativizer Rel,
        BepInExProviderIndex Index,
        bool HasBepInExContext,
        Inspection[] Results);
}
