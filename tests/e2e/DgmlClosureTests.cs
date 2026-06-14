using System;

namespace PeFix.Tests;

[Trait("Category", "E2E")]
public sealed class DgmlClosureTests : IDisposable
{
    private readonly TempDir _temp = new();

    [Fact]
    public void Closure_Dgml_EmitsDirectedGraphWithNodesAndCategories()
    {
        _temp.CopyAll("F18_missing_refs.dll");

        CliResult result = CliRunner.Run("closure", _temp.DirPath, "--dgml");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("<DirectedGraph", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("<Node ", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Category=\"Unresolved\"", result.Stdout, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
