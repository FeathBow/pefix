using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PeFix.Tests;

internal static class CliRunner
{
    // EAGAIN: transient fork exhaustion under parallel test process churn.
    private const int RetryLimit = 5;
    private const int RetryDelayMs = 200;

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

        using var process = StartWithRetry(startInfo);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, stdout, stderr);
    }

    private static Process StartWithRetry(ProcessStartInfo startInfo)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start pefix.");
            }
            catch (Win32Exception) when (attempt < RetryLimit)
            {
                Thread.Sleep(RetryDelayMs * attempt);
            }
        }
    }

    private static string GetPeFixPath()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var dllPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "PeFix", configuration, "pefix.dll"));
        if (File.Exists(dllPath))
        {
            return dllPath;
        }

        throw new FileNotFoundException($"Built pefix DLL was not found for configuration '{configuration}'.", dllPath);
    }
}

internal readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);
