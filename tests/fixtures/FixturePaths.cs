using System;
using System.IO;

namespace PeFix.Tests;

internal static class FixturePaths
{
    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string TestProjectRoot =>
        Path.Combine(RepoRoot, "tests");

    public static string Get(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", fixtureName);

    public static string GetGolden(string fileName) =>
        Path.Combine(TestProjectRoot, "fixtures", "golden", fileName);
}
