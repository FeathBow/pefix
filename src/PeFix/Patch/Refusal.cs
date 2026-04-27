using PeFix.Meta;

namespace PeFix.Patch;

public readonly record struct Refusal(
    string Path,
    string Reason,
    Inspection Before)
{
    public static Refusal Create(string path, string reason) =>
        new(System.IO.Path.GetFullPath(path), reason, PeAnalyzer.Inspect(path));
}
