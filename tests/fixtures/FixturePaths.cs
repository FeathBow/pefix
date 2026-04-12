using System;
using System.IO;

namespace PeFix.Tests;

internal static class FixturePaths
{
    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string Get(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", fixtureName);
}
