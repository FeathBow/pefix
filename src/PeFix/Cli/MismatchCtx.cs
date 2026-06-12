using PeFix.Meta;

namespace PeFix.Cli;

internal readonly record struct MismatchCtx(
    PathRelativizer Rel,
    Inspection[] Results,
    LoaderTarget? DeclaredHost,
    IReadOnlyDictionary<string, LoaderTarget> LoaderByPath);
