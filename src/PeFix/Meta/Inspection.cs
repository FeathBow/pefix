namespace PeFix.Meta;

public readonly record struct Inspection(
    string Path,
    bool ValidPe,
    bool HasCliHeader,
    string? PeFormat,
    string? Machine,
    CliFlags CliFlags,
    Signals Signals,
    Category? Category,
    Status Status,
    string PrimaryCause,
    string[] RuntimeRisks,
    string[] Warnings,
    string[] NextSteps,
    string? LoadReqs,
    string[]? PInvokeDeps);
