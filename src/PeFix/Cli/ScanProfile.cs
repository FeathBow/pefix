using PeFix.Meta;

namespace PeFix.Cli;

internal sealed record ScanProfile(
    HostProfile Host,
    string Artifact,
    LoaderTarget? DeclaredLoaderTarget = null);
