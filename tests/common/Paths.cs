using System;
using System.IO;

namespace PeFix.Tests;

internal static class Paths
{
    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string Get(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);
}
