using System.CommandLine;

namespace PeFix.Commands;

internal static class RootCmd
{
    internal static readonly Option<bool> JsonOpt = new("--json")
    {
        Description = "Write structured JSON output.",
        Recursive = true
    };

    public static RootCommand Create()
    {
        var command = new RootCommand(
            "pefix is a single-binary CLI that diagnoses and (selectively) repairs .NET assembly portability and load-failure issues.\n\n"
            + "Run `pefix <verb> --help` for verb-specific options.\n\n"
            + "Exit codes:\n"
            + "  0  Success or no gate triggered\n"
            + "  1  Failure-on-gate triggered, refusal, or non-compatible result\n"
            + "  2  Usage error\n"
            + "  4  IO error");
        command.Options.Add(JsonOpt);
        command.Subcommands.Add(InspectCmd.Create());
        command.Subcommands.Add(ScanCmd.Create());
        command.Subcommands.Add(ClosureCmd.Create());
        command.Subcommands.Add(FixCmd.Create());
        command.Subcommands.Add(SnStripCmd.Create());
        command.Subcommands.Add(RedirCmd.Create());
        command.Subcommands.Add(PinvokeCmd.Create());
        command.Subcommands.Add(PublicCmd.Create());

        return command;
    }
}
