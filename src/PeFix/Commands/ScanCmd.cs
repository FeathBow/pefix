using System.CommandLine;

namespace PeFix.Commands;

internal static class ScanCmd
{
    internal static Command Create()
    {
        var opts = new OptSet();
        var cmd = new Command("scan", "Scans a folder of managed assemblies for loadability and dependency issues.");
        opts.AddTo(cmd);
        cmd.SetAction(r => (int)Scan.Run(new Scan.ScanArgs
        {
            Path = r.GetValue(opts.PathArg)!,
            Json = r.GetValue(RootCmd.JsonOpt),
            FailOn = r.GetValue(opts.FailOnOpt),
            FailOnConflict = r.GetValue(opts.ConflictOpt),
            FailOnIssue = r.GetValue(opts.IssueOpt),
            Profile = r.GetValue(opts.ProfileOpt),
            References = r.GetValue(opts.ReferencesOpt),
            Baseline = r.GetValue(opts.BaselineOpt),
            WriteBaseline = r.GetValue(opts.WriteBaselineOpt)
        }));
        return cmd;
    }

    private sealed class OptSet
    {
        public Argument<string> PathArg { get; } = new("path")
        {
            Description = "Directory to scan."
        };

        public Option<string?> FailOnOpt { get; } = new("--fail-on")
        {
            Description = "Exit with code 1 when the result meets or exceeds the given severity."
        };

        public Option<bool> ConflictOpt { get; } = new("--fail-on-conflict")
        {
            Description = "Exit with code 1 when version conflicts are detected."
        };

        public Option<bool> IssueOpt { get; } = new("--fail-on-issue")
        {
            Description = "Exit with code 1 when any blocking directory issue or unsafe/corrupt file diagnosis is found. Use as a CI gate on publish/plugin folders."
        };

        public Option<string?> ProfileOpt { get; } = new("--profile")
        {
            Description = "Static host/artifact profile assumptions. Supported: unity-bepinex, unity-bepinex5, unity-bepinex6-mono, unity-bepinex6-il2cpp, dotnet-plugin, publish-dir."
        };

        public Option<bool> ReferencesOpt { get; } = new("--references")
        {
            Description = "Include a reference inventory in text output and JSON."
        };

        public Option<string?> BaselineOpt { get; } = new("--baseline")
        {
            Description = "Baseline file of accepted issue lines. Exit with code 1 when a blocking issue is not in the baseline; baselined issues are still reported but do not fail the gate."
        };

        public Option<bool> WriteBaselineOpt { get; } = new("--write-baseline")
        {
            Description = "Write the current blocking issue lines to the --baseline path instead of gating, then exit 0."
        };

        public void AddTo(Command cmd)
        {
            cmd.Arguments.Add(PathArg);
            cmd.Options.Add(FailOnOpt);
            cmd.Options.Add(ConflictOpt);
            cmd.Options.Add(IssueOpt);
            cmd.Options.Add(ProfileOpt);
            cmd.Options.Add(ReferencesOpt);
            cmd.Options.Add(BaselineOpt);
            cmd.Options.Add(WriteBaselineOpt);
        }
    }
}
