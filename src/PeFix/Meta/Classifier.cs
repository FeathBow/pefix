namespace PeFix.Meta;

internal static class Classifier
{
    public static Inspection Classify(PeSnapshot snapshot)
    {
        if (!snapshot.ValidPe)
        {
            return CreateBad(snapshot.Path, "This file is not a valid PE file or is corrupted.");
        }

        if (!snapshot.HasCliHeader)
        {
            return CreateNative(snapshot);
        }

        if (snapshot.Signals.IsRefAsm)
        {
            return CreateRefAsm(snapshot);
        }

        if (snapshot.Signals.IsMixedMode)
        {
            return CreateMixed(snapshot);
        }

        if (IsCompatible(snapshot))
        {
            return CreateCompat(snapshot);
        }

        return CreateFix(snapshot);
    }

    public static Inspection CreateBad(string path, string cause)
    {
        return new Inspection(
            path,
            false,
            false,
            null,
            null,
            default,
            default,
            null,
            Status.Corrupt,
            cause,
            [],
            [],
            ["This file is not a valid PE file or is corrupted. Verify the download or obtain a fresh copy."],
            null,
            null);
    }

    private static Inspection CreateNative(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            false,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.NativeBinary,
            Status.Unsafe,
            "This PE file does not contain a CLI header.",
            [],
            [],
            ["This is not a .NET assembly. It is a native executable or DLL. PE header rewriting does not apply."],
            null,
            null);
    }

    private static Inspection CreateRefAsm(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.RefAssembly,
            Status.Unsafe,
            "Reference assembly, not a runtime assembly.",
            [],
            [],
            ["Reference assembly cannot be executed. Use the runtime assembly from bin/ instead of the ref/ folder."],
            null,
            null);
    }

    private static Inspection CreateMixed(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.MixedMode,
            Status.Unsafe,
            "This assembly contains native code because ILOnly is false.",
            [],
            [],
            ["C++/CLI mixed-mode assembly. On .NET Core/.NET 5+: ensure ijwhost.dll is deployed alongside. On .NET Framework: install the VC++ redistributable."],
            null,
            null);
    }

    private static Inspection CreateCompat(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.Portability,
            Status.Compatible,
            "This assembly already uses a portable IL-only AnyCPU header.",
            [],
            [],
            ["No action needed. This assembly is already portable."],
            GetLoadReqs(snapshot),
            snapshot.PInvokeDeps);
    }

    private static Inspection CreateFix(PeSnapshot snapshot)
    {
        Status status = GetStatus(snapshot);
        string[] warnings = GetWarnings(snapshot);
        string nextStep = GetFixableNextStep(snapshot, status);

        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.Portability,
            status,
            "This assembly uses a platform-specific managed PE header.",
            GetRuntimeRisks(snapshot),
            warnings,
            [nextStep],
            GetLoadReqs(snapshot),
            snapshot.PInvokeDeps);
    }

    private static bool IsCompatible(PeSnapshot snapshot)
    {
        return snapshot.CliFlags.IlOnly
            && string.Equals(snapshot.PeFormat, "PE32", StringComparison.Ordinal)
            && string.Equals(snapshot.Machine, "I386", StringComparison.Ordinal)
            && !snapshot.CliFlags.Required32Bit;
    }

    private static Status GetStatus(PeSnapshot snapshot)
    {
        return snapshot.Signals.StrongName || snapshot.Signals.HasPInvoke
            ? Status.Cautioned
            : Status.Fixable;
    }

    private static string[] GetRuntimeRisks(PeSnapshot snapshot)
    {
        return snapshot.Signals.HasPInvoke
            ? ["Native dependencies may still fail on the target platform."]
            : [];
    }

    private static string[] GetWarnings(PeSnapshot snapshot)
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

    private static string GetFixableNextStep(PeSnapshot snapshot, Status status)
    {
        string fileName = Path.GetFileName(snapshot.Path);
        return status switch
        {
            Status.Fixable => $"Run: pefix fix {fileName}",
            Status.Cautioned when snapshot.Signals.StrongName =>
                $"Run: pefix fix --force {fileName}. Warning: the strong name signature will be invalidated. You may need to re-sign the assembly.",
            Status.Cautioned =>
                $"Run: pefix fix --force {fileName}. Warning: native dependencies (P/Invoke) may still fail on the target platform.",
            _ => "No action available."
        };
    }

    /// <summary>
    /// Returns architecture load requirement hint, or null if the assembly is AnyCPU or has no restriction.
    /// AnyCPU = IlOnly + PE32/I386 + !Required32Bit (regardless of Preferred32Bit).
    /// </summary>
    private static string? GetLoadReqs(PeSnapshot snapshot)
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
