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

        if (snapshot.HasNest)
        {
            return CreateNest(snapshot);
        }

        if (snapshot.HasRefs)
        {
            return CreateMulti(snapshot);
        }

        if (snapshot.Signals.IsMixedMode)
        {
            return CreateMixed(snapshot);
        }

        if (snapshot.R2R.HasValue)
        {
            return CreateR2R(snapshot);
        }

        if (snapshot.IsTrimmable)
        {
            return CreateTrim(snapshot);
        }

        if (snapshot.OsPlatforms is { Length: > 0 })
            return CreateOsApi(snapshot);

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
            null,
            null,
            null,
            null,
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
            null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
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
            null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
    }

    private static Inspection CreateNest(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.ModuleNest,
            Status.Unsafe,
            "This assembly contains types nested under the <Module> type. This pattern is rejected by .NET 9+ runtime and will cause BadImageFormatException.",
            [],
            [],
            ["Decompile the assembly with ILSpy and identify which obfuscator/tool created nested types under <Module>. Reprocess through a compatible version."],
            null,
            null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
    }

    private static Inspection CreateMulti(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.MultiModule,
            Status.Unsafe,
            "Multi-module assembly, not supported by .NET Core or .NET 5+.",
            [],
            [],
            ["Recompile the assembly as a single-module assembly using a modern .NET SDK."],
            null,
            null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
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
            null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
    }

    private static Inspection CreateR2R(PeSnapshot snapshot)
    {
        R2RInfo r2r = snapshot.R2R!.Value;
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.R2R,
            Status.Cautioned,
            $"This assembly contains ReadyToRun precompiled code (v{r2r.MajorVersion}.{r2r.MinorVersion}). JIT fallback occurs if the runtime version does not match the R2R compilation target.",
            [],
            [],
            ["Verify the .NET runtime version matches the R2R compilation target. Mismatched versions silently fall back to JIT. R2R code is skipped; JIT recompiles all methods."],
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef,
            true);
    }

    private static Inspection CreateTrim(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.Trimmable,
            Status.Cautioned,
            "This assembly declares itself trimmable (IsTrimmable=true). It is a candidate for IL trimming; if published with PublishTrimmed=true, types or methods may be removed.",
            [],
            [],
            ["Verify required types are preserved. Use TrimmerRootDescriptors or [DynamicDependency] attributes to protect types from trimming."],
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef,
            null,
            true);
    }

    private static Inspection CreateOsApi(PeSnapshot snapshot)
    {
        string platforms = string.Join(", ", snapshot.OsPlatforms!);
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.PlatformApi,
            Status.Unsafe,
            $"This assembly is restricted to specific OS platforms: {platforms}.",
            [],
            [],
            [$"This assembly targets {platforms}. It will throw PlatformNotSupportedException on other operating systems. Check if a cross-platform alternative exists."],
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
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
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
    }

    private static Inspection CreateFix(PeSnapshot snapshot)
    {
        Status status = GetStatus(snapshot);
        string[] warnings = ClsMessages.Warnings(snapshot);
        string nextStep = ClsMessages.NextStep(snapshot, status);

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
            ClsMessages.RuntimeRisks(snapshot),
            warnings,
            [nextStep],
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
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
}
