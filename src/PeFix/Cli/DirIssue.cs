namespace PeFix.Cli;

internal sealed record DirIssue(
    string Code,
    string Subject,
    string Summary,
    string[] Files,
    string[] NextSteps);
