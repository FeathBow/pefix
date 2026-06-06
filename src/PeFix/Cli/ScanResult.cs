namespace PeFix.Cli;

internal sealed record ScanResult(
    ScanView View,
    ScanParts? Json);
