using PeFix.Meta;

namespace PeFix.Cli;

internal static class BepIssues
{
    private const string MissingHint = "Install or restore the missing BepInEx plugin dependency.";
    private const string MissingStep = "Install the missing BepInEx plugin dependency into the scanned plugins directory.";
    private const string CaseHint = "Fix the plugin GUID casing or install the matching dependency version into the scanned plugins directory.";

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

                string hint = state is BepDepState.CaseMismatch ? CaseHint : MissingHint;
                string step = state is BepDepState.CaseMismatch ? CaseHint : MissingStep;
                build.Issues.Add(new DirIssue(
                    IssueCodeFor(state),
                    dep.Guid,
                    Summary(plugin.Guid, dep.Guid, state),
                    [build.Rel.One(result.Path)],
                    [step],
                    RepairClass.AssistedFix,
                    hint,
                    "pefix scan <path> --json",
                    ["Plugin ABI compatibility and runtime chainloader success are not proven."]));
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
