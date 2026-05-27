namespace PeFix.Cli;

internal sealed record DirectoryIssue(
    string Code,
    string Subject,
    string Summary,
    string[] Files,
    string[] NextSteps,
    string RepairClass,
    string RepairHint,
    string VerifyCommand,
    string[] UnverifiedRisks,
    IssueEvidence? Evidence = null);
