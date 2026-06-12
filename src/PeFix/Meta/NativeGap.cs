namespace PeFix.Meta;

public readonly record struct NativeGap(
    string ModuleName,
    string ConsumerPath,
    string? PresentPath,
    string? PresentMachine,
    string? RequiredMachine);
