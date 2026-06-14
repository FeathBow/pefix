using System;
using System.IO;
using System.Linq;
using PeFix.Meta;

namespace PeFix.Tests;

// Standing precision suite over REAL publish artifacts (see RealPublishMatrix). Encodes
// the invariants the synthetic unit tests could not: valid publishes are 0-FP, every
// command agrees on the same input, and a genuinely missing dependency still fails.
[Collection("real-publish")]
[Trait("Category", "E2E")]
public sealed class PrecisionMatrixTests
{
    private readonly RealPublishMatrix _matrix;

    public PrecisionMatrixTests(RealPublishMatrix matrix) => _matrix = matrix;

    [Fact]
    public void ValidPublishes_AreZeroFalsePositive_AtTheGate()
    {
        SkipIfUnavailable();
        foreach (PublishCase shape in _matrix.Cases)
        {
            CliResult result = CliRunner.Run("scan", shape.Dir, "--profile", "publish-dir", "--fail-on-issue");
            Assert.True(
                result.ExitCode == 0,
                $"False positive: valid publish '{shape.Name}' failed the gate.\n{result.Stdout}\n{result.Stderr}");
        }
    }

    [Fact]
    public void ValidPublishes_AreConsistentAcrossScanClosureAndInventory()
    {
        SkipIfUnavailable();
        foreach (PublishCase shape in _matrix.Cases)
        {
            IReadOnlySet<string>? declared = DepsReader.ReadDeclaredAssets(shape.Dir);
            DirectoryInspection dir = Scanner.InspectDir(shape.Dir);
            DependencyIndex deps = DependencyIndex.Build(dir.Results, HostProfile.DotNet, declared);

            MissingReference[] scanMissing = deps.FindMissingReferences(dir.Results);
            ClosureReport closure = ClosureGraph.Build(dir.Results, shape.Dir, HostProfile.DotNet, declared);
            RefEntry[] inventory = RefInventory.Collect(dir.Results, HostProfile.DotNet, declared);

            Assert.True(scanMissing.Length == 0, $"{shape.Name}: scan reports missing {Join(scanMissing.Select(m => m.ReferenceName))}");
            Assert.True(closure.Unresolved.Length == 0, $"{shape.Name}: closure reports unresolved leaves");
            Assert.DoesNotContain(inventory, entry => entry.Status is RefStatus.Missing or RefStatus.VersionConflict);
        }
    }

    [Fact]
    public void TamperedPublish_FailsTheGate_OnTheRemovedDependency()
    {
        SkipIfUnavailable();
        PublishCase? target = _matrix.Cases.FirstOrDefault(shape => shape.RemovableDep is not null);
        if (target is null)
        {
            Assert.Skip("No removable-dependency case in the matrix.");
            return;
        }

        using var temp = new TempDir();
        CopyInto(target.Dir, temp.DirPath);
        File.Delete(Path.Combine(temp.DirPath, target.RemovableDep!));

        CliResult result = CliRunner.Run("scan", temp.DirPath, "--profile", "publish-dir", "--fail-on-issue");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            Path.GetFileNameWithoutExtension(target.RemovableDep!),
            result.Stdout,
            StringComparison.OrdinalIgnoreCase);
    }

    private void SkipIfUnavailable()
    {
        if (_matrix.Cases.Count == 0)
            Assert.Skip("dotnet SDK unavailable; real-publish precision matrix not built.");
    }

    private static string Join(IEnumerable<string> names) => string.Join(", ", names);

    private static void CopyInto(string sourceDir, string destDir)
    {
        foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string target = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
