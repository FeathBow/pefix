namespace PeFix.Meta;

internal static class ClsMessages
{
    internal static string[] RuntimeRisks(PeSnapshot snapshot)
    {
        return snapshot.Signals.HasPInvoke
            ? ["Native dependencies may still fail on the target platform."]
            : [];
    }

    internal static string[] Warnings(PeSnapshot snapshot)
    {
        if (snapshot.Signals.StrongName && snapshot.Signals.HasPInvoke)
        {
            return
            [
                "The strong name signature will be invalidated by patching.",
                "Native dependencies may still fail on the target platform."
            ];
        }

        if (snapshot.Signals.StrongName)
        {
            return ["The strong name signature will be invalidated by patching."];
        }

        if (snapshot.Signals.HasPInvoke)
        {
            return ["Native dependencies may still fail on the target platform."];
        }

        return [];
    }

    internal static string NextStep(PeSnapshot snapshot, Status status)
    {
        string quotedPath = QuotePath(snapshot.Path);
        return status switch
        {
            Status.Fixable => $"Run: pefix {quotedPath} --fix",
            Status.Cautioned when snapshot.Signals.StrongName =>
                $"Run: pefix {quotedPath} --fix --force. Warning: the strong name signature will be invalidated. You may need to re-sign the assembly.",
            Status.Cautioned =>
                $"Run: pefix {quotedPath} --fix --force. Warning: native dependencies (P/Invoke) may still fail on the target platform.",
            _ => "No action available."
        };
    }

    private static string QuotePath(string path)
    {
        bool needsQuoting = path.Any(c => c == ' ' || c == '\t' || c == '\'' || c == '"' || c == '\\' || c == '$' || c == '`');
        if (!needsQuoting)
        {
            return path;
        }

        if (OperatingSystem.IsWindows())
        {
            return $"\"{path.Replace("\"", "\\\"")}\"";
        }

        return $"'{path.Replace("'", "'\\''")}'";
    }

    internal static string? LoadReqs(PeSnapshot snapshot)
    {
        // True AnyCPU: IlOnly + I386 + not Required32Bit — no host architecture restriction.
        if (snapshot.CliFlags.IlOnly
            && string.Equals(snapshot.Machine, "I386", StringComparison.Ordinal)
            && !snapshot.CliFlags.Required32Bit)
        {
            return null;
        }

        // AnyCPU Prefer32Bit variant — still loads in both 32/64-bit, no restriction.
        if (snapshot.CliFlags.IlOnly && snapshot.CliFlags.Preferred32Bit)
        {
            return null;
        }

        return snapshot.Machine switch
        {
            "AMD64" or "ARM64" => "Requires 64-bit host process. Fails with BadImageFormatException in 32-bit processes (e.g. x86 test runner, 32-bit IIS).",
            "I386" => "Requires 32-bit host process. Fails in 64-bit-only environments.",
            _ => null
        };
    }
}
