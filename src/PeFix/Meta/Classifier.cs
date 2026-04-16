namespace PeFix.Meta;

internal static class Classifier
{
    public static Inspection Classify(PeSnapshot snapshot)
    {
        if (!snapshot.ValidPe)
            return CreateBad(snapshot.Path, "This file is not a valid PE file or is corrupted.");
        if (!snapshot.HasCliHeader)
            return CreateNative(snapshot);
        return ClassifyCli(snapshot);
    }

    private static Inspection ClassifyCli(PeSnapshot snapshot)
    {
        if (snapshot.Signals.IsRefAsm) return CreateRefAsm(snapshot);
        if (snapshot.IsSatellite) return CreateSat(snapshot);
        if (snapshot.HasNest) return CreateNest(snapshot);
        if (snapshot.HasRefs) return CreateMulti(snapshot);
        if (snapshot.Signals.IsMixedMode) return CreateMixed(snapshot);
        if (IsLegacyTfm(snapshot.Tfm)) return CreateTfmBad(snapshot);
        if (snapshot.R2R.HasValue) return CreateR2R(snapshot);
        if (snapshot.IsTrimmable) return CreateTrim(snapshot);
        if (snapshot.IsBundle) return CreateBundle(snapshot);
        if (snapshot.OsPlatforms is { Length: > 0 }) return CreateOsApi(snapshot);
        return IsCompatible(snapshot) ? CreateCompat(snapshot) : CreateFix(snapshot);
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
            ReasonCode.CorruptPe,
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

    public static Inspection CreateWebcil(string path)
    {
        return new Inspection(
            path,
            false,
            false,
            null,
            null,
            default,
            default,
            Category.Webcil,
            Status.Unsafe,
            ReasonCode.Webcil,
            "This file starts with WebAssembly magic bytes (\\0asm). In a .dll context this indicates a Blazor Webcil package.",
            [],
            [],
            ["Webcil wraps IL in a WebAssembly container for browser delivery. The file cannot be inspected or patched as a standard PE file."],
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
            ReasonCode.NativeBinary,
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
            ReasonCode.RefAssembly,
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

    private static Inspection CreateSat(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.Satellite,
            Status.Unsafe,
            ReasonCode.Satellite,
            ".NET satellite assembly containing only localized resources.",
            [],
            [],
            ["Satellite assemblies hold culture-specific resources and contain no executable IL. PE header patching does not apply. Deploy alongside the main assembly."],
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
            ReasonCode.ModuleNest,
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
            ReasonCode.MultiModule,
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
            ReasonCode.MixedMode,
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
            ReasonCode.R2R,
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
            ReasonCode.Trimmable,
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

    private static Inspection CreateBundle(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.Bundle,
            Status.Cautioned,
            ReasonCode.Bundle,
            "This executable is a .NET single-file bundle with embedded assemblies.",
            [],
            [],
            ["Single-file bundles embed all assemblies inside the host EXE. Patching the outer PE header does not affect embedded assemblies. Use 'dotnet publish --no-self-contained' or patch each embedded assembly individually."],
            ClsMessages.LoadReqs(snapshot),
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
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
            ReasonCode.PlatformApi,
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
            ReasonCode.Portable,
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
            ReasonCode.NonPortable,
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

    private static Inspection CreateTfmBad(PeSnapshot snapshot)
    {
        return new Inspection(
            snapshot.Path,
            true,
            true,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.CliFlags,
            snapshot.Signals,
            Category.TfmMismatch,
            Status.Unsafe,
            ReasonCode.TfmMismatch,
            $"This assembly targets {snapshot.Tfm} (.NET Framework) and will not load on .NET Core or .NET 5+.",
            [],
            [],
            ["Recompile targeting netstandard2.0 or net8.0+. .NET Framework assemblies are not compatible with CoreCLR."],
            null,
            snapshot.PInvokeDeps,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyRefs,
            snapshot.AssemblyDef);
    }

    // Legacy .NET Framework TFMs are "net" + digits only (e.g. net48, net472, net40).
    // Modern .NET TFMs always contain a dot in the version segment (e.g. net10.0, net8.0).
    private static bool IsLegacyTfm(string? tfm)
    {
        if (tfm is null) return false;
        if (!tfm.StartsWith("net", StringComparison.Ordinal)) return false;
        string rest = tfm[3..];
        return rest.Length > 0 && char.IsAsciiDigit(rest[0]) && !rest.Contains('.');
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
