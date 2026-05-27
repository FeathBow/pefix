namespace PeFix.Cli;

internal sealed record BepInExPluginProvider(
    string Guid,
    string Version,
    string File);
