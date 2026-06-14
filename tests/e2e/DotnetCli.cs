using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace PeFix.Tests;

// Runs the `dotnet` SDK CLI as a subprocess to produce REAL publish artifacts for the
// precision matrix. Mirrors CliRunner's EAGAIN retry; disables MSBuild node reuse to
// limit fork pressure when many publishes run back to back.
internal static class DotnetCli
{
    private const int RetryLimit = 5;
    private const int RetryDelayMs = 200;

    public static bool Available { get; } = Probe();

    public static DotnetResult Run(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        using Process process = StartWithRetry(startInfo);
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new DotnetResult(process.ExitCode, stdout + stderr);
    }

    private static Process StartWithRetry(ProcessStartInfo startInfo)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
            }
            catch (Win32Exception) when (attempt < RetryLimit)
            {
                Thread.Sleep(RetryDelayMs * attempt);
            }
        }
    }

    private static bool Probe()
    {
        try
        {
            var startInfo = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using Process process = Process.Start(startInfo)!;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

internal readonly record struct DotnetResult(int ExitCode, string Output);
