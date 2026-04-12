using System;
using System.Diagnostics;
using System.IO;

namespace PeFix.Tests;

internal static class CliRunner
{
    public static CliResult Run(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(GetPeFixPath());
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start pefix.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, stdout, stderr);
    }

    private static string GetPeFixPath()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var dllPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "PeFix", configuration, "PeFix.dll"));
        if (File.Exists(dllPath))
        {
            return dllPath;
        }

        throw new FileNotFoundException($"Built pefix DLL was not found for configuration '{configuration}'.", dllPath);
    }
}

internal readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);
