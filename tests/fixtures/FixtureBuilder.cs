using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace PeFix.Tests.Fixtures;

internal static class FixtureBuilder
{
    private static readonly (string Project, string Output)[] SourceFixtures =
    [
        ("CompatibleAnyCpu/CompatibleAnyCpu.csproj", "F01_compatible_anycpu.dll"),
        ("X64OnlyManaged/X64OnlyManaged.csproj", "F02_x64only_managed.dll"),
        ("X64StrongName/X64StrongName.csproj", "F03_x64_strongname.dll"),
        ("X64PInvoke/X64PInvoke.csproj", "F04_x64_pinvoke.dll"),
        ("RefAssembly/RefAssembly.csproj", "F05_reference_assembly.dll")
    ];

    public static void BuildAll(string testProjectRoot)
    {
        var outputDir = Path.Combine(testProjectRoot, "output", "fixtures");
        Directory.CreateDirectory(outputDir);
        BuildSourceFixtures(testProjectRoot, outputDir);
        BuildDerivedFixtures(outputDir);
    }

    private static void BuildSourceFixtures(string testProjectRoot, string outputDir)
    {
        var sourcesRoot = Path.Combine(testProjectRoot, "fixtures", "sources");
        foreach (var fixture in SourceFixtures)
        {
            var projectPath = Path.Combine(sourcesRoot, fixture.Project);
            RunDotNetBuild(projectPath);
            CopyFixture(projectPath, Path.Combine(outputDir, fixture.Output));
        }
    }

    private static void BuildDerivedFixtures(string outputDir)
    {
        var sourcePath = Path.Combine(outputDir, "F02_x64only_managed.dll");
        CreateMixedModeFixture(sourcePath, Path.Combine(outputDir, "F06_mixed_mode.dll"));
        CreateNativePeFixture(sourcePath, Path.Combine(outputDir, "F07_native_pe.dll"));
        CreateCorruptFixture(Path.Combine(outputDir, "F01_compatible_anycpu.dll"), Path.Combine(outputDir, "F08_corrupt.dll"));
        File.WriteAllBytes(Path.Combine(outputDir, "F09_empty.dll"), []);
    }

    private static void CopyFixture(string projectPath, string targetPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Project directory was not found.");
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var outputPath = Path.Combine(projectDirectory, "bin", "Release", "net10.0", $"{projectName}.dll");
        File.Copy(outputPath, targetPath, overwrite: true);
    }

    private static void RunDotNetBuild(string projectPath)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet build.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException($"Fixture build failed for {projectPath}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }

    private static void CreateMixedModeFixture(string sourcePath, string targetPath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        using var stream = new MemoryStream(bytes, writable: true);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var offset = reader.PEHeaders.CorHeaderStartOffset + 16;
        var flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), flags & ~(int)CorFlags.ILOnly);
        File.WriteAllBytes(targetPath, bytes);
    }

    private static void CreateNativePeFixture(string sourcePath, string targetPath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        using var stream = new MemoryStream(bytes, writable: true);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var directoryOffset = GetCorHeaderDirectoryEntryOffset(reader.PEHeaders);
        bytes.AsSpan(directoryOffset, 8).Clear();
        File.WriteAllBytes(targetPath, bytes);
    }

    private static void CreateCorruptFixture(string sourcePath, string targetPath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var truncatedLength = Math.Min(100, bytes.Length);
        File.WriteAllBytes(targetPath, bytes.AsSpan(0, truncatedLength).ToArray());
    }

    private static int GetCorHeaderDirectoryEntryOffset(PEHeaders headers)
    {
        var dataDirectoriesOffset = headers.PEHeaderStartOffset + GetDataDirectoriesOffset(headers.PEHeader!.Magic);
        return dataDirectoriesOffset + (14 * 8);
    }

    private static int GetDataDirectoriesOffset(PEMagic magic)
    {
        return magic == PEMagic.PE32 ? 96 : 112;
    }
}
