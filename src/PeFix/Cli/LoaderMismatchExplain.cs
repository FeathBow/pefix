using PeFix.Meta;

namespace PeFix.Cli;

internal static class LoaderMismatchExplain
{
    public static MismatchResult Explain(MismatchCtx input)
    {
        List<(string Path, LoaderTarget Target)> plugins = [];
        foreach (Inspection result in input.Results)
        {
            if (result.BepInEx is not { Plugins.Length: > 0 })
                continue;

            LoaderTarget target = input.LoaderByPath[result.Path];
            if (target.IsBepInExTarget)
                plugins.Add((result.Path, target));
        }

        if (plugins.Count == 0)
            return new([], []);

        LoaderTarget host = input.DeclaredHost is { IsBepInExTarget: true } declared
            ? declared
            : DetectHost(input.Results);
        if (host.IsBepInExTarget)
        {
            List<(string Path, LoaderTarget Target)> offenders =
                [.. plugins.Where(item => !item.Target.IsCompatibleWith(host))];
            if (offenders.Count > 0)
                return EmitMismatch(input.Rel, offenders, host);

            return new([], []);
        }

        if (HasMixedTargets(plugins))
            return EmitMismatch(input.Rel, plugins, LoaderTarget.None);

        return new([], []);
    }

    internal static LoaderTarget DetectHost(Inspection[] results)
    {
        List<AssemblyIdentity> hostDefinitions = [];
        foreach (Inspection result in results)
        {
            if (result.BepInEx is { Plugins.Length: > 0 })
                continue;

            if (result.AssemblyDefinition is { } definition)
                hostDefinitions.Add(definition);
        }

        return LoaderTargetReader.FromReferences(hostDefinitions);
    }

    private static MismatchResult EmitMismatch(
        PathRelativizer rel,
        IReadOnlyList<(string Path, LoaderTarget Target)> flagged,
        LoaderTarget host)
    {
        string[] files = [.. flagged
            .Select(item => rel.RelativePath(item.Path))
            .OrderBy(path => path, StringComparer.Ordinal)];
        string[] blockedPaths = [.. flagged.Select(item => item.Path)];

        return new MismatchResult(
            [
                RepairGuide.ForIssue(
                    IssueCode.BepLoaderMismatch,
                    "loader target",
                    host.IsBepInExTarget
                        ? HostMismatchSummary(rel, flagged, host)
                        : IntraFolderSummary(rel, flagged),
                    files)
            ],
            blockedPaths);
    }

    private static bool HasMixedTargets(IReadOnlyList<(string Path, LoaderTarget Target)> plugins)
    {
        bool gen5 = plugins.Any(item => item.Target.Generation == LoaderGeneration.BepInEx5);
        bool gen6 = plugins.Any(item => item.Target.Generation == LoaderGeneration.BepInEx6);
        bool mono = plugins.Any(item => item.Target.Flavor == LoaderFlavor.Mono);
        bool il2cpp = plugins.Any(item => item.Target.Flavor == LoaderFlavor.Il2Cpp);
        return (gen5 && gen6) || (mono && il2cpp);
    }

    private static string IntraFolderSummary(
        PathRelativizer rel,
        IReadOnlyList<(string Path, LoaderTarget Target)> plugins)
    {
        return "Plugins in this folder target incompatible BepInEx loaders; a single host loads only one. "
            + GroupByTarget(rel, plugins) + ".";
    }

    private static string HostMismatchSummary(
        PathRelativizer rel,
        IReadOnlyList<(string Path, LoaderTarget Target)> plugins,
        LoaderTarget host)
    {
        return $"The scanned loader is {Describe(host)}, but these plugins target an incompatible loader and will be skipped: "
            + GroupByTarget(rel, plugins) + ".";
    }

    private static string GroupByTarget(
        PathRelativizer rel,
        IReadOnlyList<(string Path, LoaderTarget Target)> plugins)
    {
        return string.Join("; ", plugins
            .GroupBy(item => Describe(item.Target), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {string.Join(", ", group
                .Select(item => rel.RelativePath(item.Path))
                .OrderBy(path => path, StringComparer.Ordinal))}"));
    }

    private static string Describe(LoaderTarget target)
    {
        string generation = target.Generation switch
        {
            LoaderGeneration.BepInEx5 => "BepInEx 5",
            LoaderGeneration.BepInEx6 => "BepInEx 6",
            _ => "BepInEx (unknown generation)",
        };
        string flavor = target.Flavor switch
        {
            LoaderFlavor.Mono => "Mono",
            LoaderFlavor.Il2Cpp => "IL2CPP",
            _ => "unknown flavor",
        };
        return $"{generation} ({flavor})";
    }
}

