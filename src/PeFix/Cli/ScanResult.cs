namespace PeFix.Cli;

internal sealed record ScanResult(
    ScanView View,
    ScanJsonParts? Json);
