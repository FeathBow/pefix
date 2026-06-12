namespace PeFix.Meta;

internal static class Classifier
{
    public static Inspection Classify(PeSnapshot snapshot)
    {
        if (!snapshot.ValidPe)
            return CreateBad(snapshot.Path, "This file is not a valid PE file or is corrupted.");
        if (!snapshot.HasCliHeader)
            return CreateNative(snapshot);
        return ClassifyCli(snapshot) with { BepInEx = snapshot.BepInEx };
    }

    private static Inspection ClassifyCli(PeSnapshot snapshot)
    {
        if (snapshot.Signals.IsRefAsm) return CreateRefAsm(snapshot);
        if (snapshot.IsSatellite) return CreateSat(snapshot);
        if (snapshot.HasNest) return CreateNest(snapshot);
        if (snapshot.HasRefs) return CreateMulti(snapshot);
        if (snapshot.Signals.IsMixedMode) return CreateMixed(snapshot);
        if (IsLegacyTfm(snapshot.Tfm)) return CreateTfmBad(snapshot);
        if (snapshot.ReadyToRun.HasValue) return CreateR2R(snapshot);
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

    private static Inspection From(PeSnapshot snapshot, Verdict verdict)
    {
        return new Inspection(
            snapshot.Path,
            true,
            verdict.HasCliHeader,
            snapshot.PeFormat,
            snapshot.Machine,
            snapshot.ManagedCorFlags,
            snapshot.Signals,
            verdict.Category,
            verdict.Status,
            verdict.ReasonCode,
            verdict.PrimaryCause,
            verdict.RuntimeRisks ?? [],
            verdict.Warnings ?? [],
            verdict.NextSteps,
            verdict.WithLoadReqs ? ClassificationMessages.LoadReqs(snapshot) : null,
            verdict.WithPInvoke ? snapshot.PInvokeDeps : null,
            snapshot.Tfm,
            snapshot.MetaVersion,
            snapshot.OsPlatforms,
            snapshot.AssemblyReferences,
            snapshot.AssemblyDefinition,
            verdict.HasReadyToRun,
            verdict.IsTrimmable);
    }

    private static Inspection CreateNative(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.NativeBinary,
            Status.Unsafe,
            ReasonCode.NativeBinary,
            "This PE file does not contain a CLI header.",
            ["This is not a .NET assembly. It is a native executable or DLL. PE header rewriting does not apply."])
        {
            HasCliHeader = false
        });
    }

    private static Inspection CreateRefAsm(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.RefAssembly,
            Status.Unsafe,
            ReasonCode.RefAssembly,
            "Reference assembly, not a runtime assembly.",
            ["Reference assembly cannot be executed. Use the runtime assembly from bin/ instead of the ref/ folder."]));
    }

    private static Inspection CreateSat(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.Satellite,
            Status.Unsafe,
            ReasonCode.Satellite,
            ".NET satellite assembly containing only localized resources.",
            ["Satellite assemblies hold culture-specific resources and contain no executable IL. PE header patching does not apply. Deploy alongside the main assembly."]));
    }

    private static Inspection CreateNest(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.ModuleNest,
            Status.Unsafe,
            ReasonCode.ModuleNest,
            "This assembly contains types nested under the <Module> type. This pattern is rejected by .NET 9+ runtime and will cause BadImageFormatException.",
            ["Decompile the assembly with ILSpy and identify which obfuscator/tool created nested types under <Module>. Reprocess through a compatible version."]));
    }

    private static Inspection CreateMulti(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.MultiModule,
            Status.Unsafe,
            ReasonCode.MultiModule,
            "Multi-module assembly, not supported by .NET Core or .NET 5+.",
            ["Recompile the assembly as a single-module assembly using a modern .NET SDK."]));
    }

    private static Inspection CreateMixed(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.MixedMode,
            Status.Unsafe,
            ReasonCode.MixedMode,
            "This assembly contains native code because ILOnly is false.",
            ["C++/CLI mixed-mode assembly. On .NET Core/.NET 5+: ensure ijwhost.dll is deployed alongside. On .NET Framework: install the VC++ redistributable."]));
    }

    private static Inspection CreateR2R(PeSnapshot snapshot)
    {
        ReadyToRunInfo r2r = snapshot.ReadyToRun!.Value;
        return From(snapshot, new Verdict(
            Category.R2R,
            Status.Cautioned,
            ReasonCode.R2R,
            $"This assembly contains ReadyToRun precompiled code (v{r2r.MajorVersion}.{r2r.MinorVersion}). JIT fallback occurs if the runtime version does not match the R2R compilation target.",
            ["Verify the .NET runtime version matches the R2R compilation target. Mismatched versions silently fall back to JIT. R2R code is skipped; JIT recompiles all methods."])
        {
            WithLoadReqs = true,
            WithPInvoke = true,
            HasReadyToRun = true
        });
    }

    private static Inspection CreateTrim(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.Trimmable,
            Status.Cautioned,
            ReasonCode.Trimmable,
            "This assembly declares itself trimmable (IsTrimmable=true). It is a candidate for IL trimming; if published with PublishTrimmed=true, types or methods may be removed.",
            ["Verify required types are preserved. Use TrimmerRootDescriptors or [DynamicDependency] attributes to protect types from trimming."])
        {
            WithLoadReqs = true,
            WithPInvoke = true,
            IsTrimmable = true
        });
    }

    private static Inspection CreateBundle(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.Bundle,
            Status.Cautioned,
            ReasonCode.Bundle,
            "This executable is a .NET single-file bundle with embedded assemblies.",
            ["Single-file bundles embed all assemblies inside the host EXE. Patching the outer PE header does not affect embedded assemblies. Use 'dotnet publish --no-self-contained' or patch each embedded assembly individually."])
        {
            WithLoadReqs = true,
            WithPInvoke = true
        });
    }

    private static Inspection CreateOsApi(PeSnapshot snapshot)
    {
        string platforms = string.Join(", ", snapshot.OsPlatforms!);
        return From(snapshot, new Verdict(
            Category.PlatformApi,
            Status.Unsafe,
            ReasonCode.PlatformApi,
            $"This assembly is restricted to specific OS platforms: {platforms}.",
            [$"This assembly targets {platforms}. It will throw PlatformNotSupportedException on other operating systems. Check if a cross-platform alternative exists."])
        {
            WithLoadReqs = true,
            WithPInvoke = true
        });
    }

    private static Inspection CreateCompat(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.Portability,
            Status.Compatible,
            ReasonCode.Portable,
            "This assembly already uses a portable IL-only AnyCPU header.",
            ["No action needed. This assembly is already portable."])
        {
            WithLoadReqs = true,
            WithPInvoke = true
        });
    }

    private static Inspection CreateFix(PeSnapshot snapshot)
    {
        Status status = GetStatus(snapshot);
        return From(snapshot, new Verdict(
            Category.Portability,
            status,
            ReasonCode.NonPortable,
            "This assembly uses a platform-specific managed PE header.",
            [ClassificationMessages.NextStep(snapshot, status)])
        {
            RuntimeRisks = ClassificationMessages.RuntimeRisks(snapshot),
            Warnings = ClassificationMessages.Warnings(snapshot),
            WithLoadReqs = true,
            WithPInvoke = true
        });
    }

    private static Inspection CreateTfmBad(PeSnapshot snapshot)
    {
        return From(snapshot, new Verdict(
            Category.TfmMismatch,
            Status.Unsafe,
            ReasonCode.TfmMismatch,
            $"This assembly targets {snapshot.Tfm} (.NET Framework) and will not load on .NET Core or .NET 5+.",
            ["Recompile targeting netstandard2.0 or net8.0+. .NET Framework assemblies are not compatible with CoreCLR."])
        {
            WithPInvoke = true
        });
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
        return snapshot.ManagedCorFlags.IlOnly
            && string.Equals(snapshot.PeFormat, "PE32", StringComparison.Ordinal)
            && string.Equals(snapshot.Machine, "I386", StringComparison.Ordinal)
            && !snapshot.ManagedCorFlags.Required32Bit;
    }

    private static Status GetStatus(PeSnapshot snapshot)
    {
        return snapshot.Signals.StrongName || snapshot.Signals.HasPInvoke
            ? Status.Cautioned
            : Status.Fixable;
    }

    private readonly record struct Verdict(
        Category Category,
        Status Status,
        string ReasonCode,
        string PrimaryCause,
        string[] NextSteps)
    {
        public bool HasCliHeader { get; init; } = true;

        public bool WithLoadReqs { get; init; }

        public bool WithPInvoke { get; init; }

        public string[]? RuntimeRisks { get; init; }

        public string[]? Warnings { get; init; }

        public bool? HasReadyToRun { get; init; }

        public bool? IsTrimmable { get; init; }
    }
}
