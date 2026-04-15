using System;
using System.IO;

namespace PeFix.Tests;

internal sealed class TempDir : IDisposable
{
    public string DirPath { get; } = Path.Combine(Path.GetTempPath(), "pefix-test-" + Guid.NewGuid().ToString("N")[..8]);

    public TempDir()
    {
        Directory.CreateDirectory(DirPath);
    }

    public string Copy(string name)
    {
        var sourcePath = Paths.Get(name);
        var destPath = Path.Combine(DirPath, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destPath, overwrite: true);
        return destPath;
    }

    public void CopyAll(params string[] names)
    {
        foreach (var name in names)
        {
            Copy(name);
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
