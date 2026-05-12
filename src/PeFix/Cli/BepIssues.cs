using PeFix.Meta;

namespace PeFix.Cli;

internal static class BepIssues
{
    private const string Hint = "Install the missing BepInEx plugin dependency into the scanned plugins directory.";

    public static DirIssue[] Build(Inspection[] results, ScanRel rel, BepIndex index)
    {
        List<DirIssue> issues = [];
        IssueBuild build = new(issues, rel, index);
        foreach (Inspection result in results)
            AddResult(build, result);

        return [.. issues];
    }

    private static void AddResult(IssueBuild build, Inspection result)
    {
        if (!result.Bep.HasValue)
            return;

        foreach (BepPlugin plugin in result.Bep.Value.Plugins)
        {
            foreach (BepDep dep in plugin.Deps)
            {
                if (!dep.Hard)
                    continue;

                BepDepState state = build.Index.Status(dep.Guid);
                if (state is BepDepState.Present)
                    continue;

                build.Issues.Add(new DirIssue(
                    IssueCode.BepMissing,
                    dep.Guid,
                    Summary(plugin.Guid, dep.Guid, state),
                    [build.Rel.One(result.Path)],
                    [Hint]));
            }
        }
    }

    private static string Summary(string plugin, string dep, BepDepState state)
    {
        string reason = state is BepDepState.CaseMismatch
            ? "only a case-different plugin GUID was found"
            : "no matching plugin GUID was found";
        return $"{plugin} requires BepInEx plugin {dep}, but {reason}.";
    }

    private readonly record struct IssueBuild(List<DirIssue> Issues, ScanRel Rel, BepIndex Index);
}
