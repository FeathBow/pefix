using System;
using System.IO;

namespace PeFix.Tests;

internal sealed class TempFixture : IDisposable
{
    public string DirPath { get; } = Path.Combine(Path.GetTempPath(), "pefix-test-" + Guid.NewGuid().ToString("N")[..8]);

    public TempFixture()
    {
        Directory.CreateDirectory(DirPath);
    }

    public string CopyFixture(string fixtureName)
    {
        var sourcePath = FixturePaths.Get(fixtureName);
        var destPath = Path.Combine(DirPath, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destPath, overwrite: true);
        return destPath;
    }

    public void CopyFixtures(params string[] fixtureNames)
    {
        foreach (var fixtureName in fixtureNames)
        {
            CopyFixture(fixtureName);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(DirPath))
        {
            Directory.Delete(DirPath, recursive: true);
        }
    }
}
