using PeFix.Meta;

namespace PeFix.Cli;

internal static class BepIssues
{
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

                build.Issues.Add(RepairGuide.ForIssue(new RepairGuide.IssueFacts
                {
                    Code = IssueCodeFor(state),
                    Subject = dep.Guid,
                    Summary = Summary(plugin.Guid, dep.Guid, state),
                    Files = [build.Rel.One(result.Path)]
                }));
            }
        }
    }

    private static string IssueCodeFor(BepDepState state)
    {
        return state is BepDepState.CaseMismatch
            ? IssueCode.BepCasing
            : IssueCode.BepMissing;
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
