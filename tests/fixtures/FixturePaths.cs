using System;
using System.IO;

namespace PeFix.Tests;

internal static class FixturePaths
{
    private static readonly object Sync = new();
    private static bool s_ready;

    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string TestProjectRoot =>
        Path.Combine(RepoRoot, "tests");

    public static string Get(string fixtureName)
    {
        EnsureBuilt();
        return Path.Combine(TestProjectRoot, "output", "fixtures", fixtureName);
    }

    public static string GetGolden(string fileName)
    {
        return Path.Combine(TestProjectRoot, "fixtures", "golden", fileName);
    }

    private static void EnsureBuilt()
    {
        lock (Sync)
        {
            if (s_ready)
            {
                return;
            }

            FixtureBuilder.BuildAll(TestProjectRoot);
            s_ready = true;
        }
    }
}
