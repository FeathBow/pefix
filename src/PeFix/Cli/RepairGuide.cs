using PeFix.Meta;

namespace PeFix.Cli;

internal static class RepairGuide
{
    private const string VerifyScan = "pefix scan <path> --json";
    private const string BepCasingStep = "Fix the plugin GUID casing or install a plugin with the exact dependency GUID into the scanned plugins directory.";

    public static RepairInfo ForInspect(Inspection result)
    {
        if (string.Equals(result.ReasonCode, ReasonCode.NonPortable, StringComparison.Ordinal))
            return NonPortable(result);

        return result.ReasonCode switch
        {
            ReasonCode.R2R => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Verify the .NET runtime version matches the R2R compilation target before changing the artifact."),
            ReasonCode.Trimmable => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Verify required types are preserved before trimming."),
            ReasonCode.Bundle => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Use a non-bundled assembly or inspect the embedded assembly separately."),
            ReasonCode.PlatformApi => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "Find a cross-platform alternative or target matching operating systems."),
            ReasonCode.CorruptPe
                or ReasonCode.Webcil
                or ReasonCode.NativeBinary
                or ReasonCode.RefAssembly
                or ReasonCode.Satellite
                or ReasonCode.ModuleNest
                or ReasonCode.MultiModule
                or ReasonCode.MixedMode
                or ReasonCode.Portable
                or ReasonCode.TfmMismatch => new RepairInfo(
                RepairClass.DiagnosticOnly,
                "No repair action is available for this diagnosis."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.ReasonCode,
                "Unsupported reason code.")
        };
    }

    public static DirectoryIssue ForIssue(
        string code,
        string subject,
        string summary,
        string[] files,
        IssueEvidence? evidence = null)
    {
        var repair = IssueRepair.For(code);
        return new DirectoryIssue(
            code,
            subject,
            summary,
            files,
            repair.NextSteps,
            repair.Class,
            repair.RepairHint,
            repair.VerifyCommand,
            repair.UnverifiedRisks,
            evidence);
    }

    private static RepairInfo NonPortable(Inspection result)
    {
        string repairClass = result.Status == Status.Fixable
            ? RepairClass.AutoFix
            : RepairClass.GuidedFix;
        return new RepairInfo(
            repairClass,
            NextStepOr(result, "Run pefix fix <path> --apply to rewrite the PE header."));
    }

    private static string NextStepOr(Inspection result, string fallback)
    {
        return result.NextSteps.Length > 0 ? result.NextSteps[0] : fallback;
    }

    private sealed class IssueRepair
    {
        public required string[] NextSteps { get; init; }
        public required string Class { get; init; }
        public required string RepairHint { get; init; }
        public required string VerifyCommand { get; init; }
        public required string[] UnverifiedRisks { get; init; }

        public static IssueRepair For(string code)
        {
            return code switch
            {
                IssueCode.AsmConflict => Assisted(
                    "Align the directory to one assembly version for this name.",
                    "Remove the mismatched copy or install the version required by the referencing assembly.",
                    "API compatibility between aligned assembly versions is not proven."),
                IssueCode.MissingRef => Assisted(
                    "Install or restore the missing managed dependency.",
                    "Install the missing managed dependency into the scanned directory or restore the package that should provide it.",
                    "API compatibility and runtime load success are not proven."),
                IssueCode.MissingType => Assisted(
                    "Align the referencing assembly and provider assembly so the referenced type exists.",
                    "Install provider and consumer assemblies built against the same type surface, or restore the package version that exposes the referenced type.",
                    "Forwarded type resolution, API compatibility, and runtime load success are not proven."),
                IssueCode.MissingMember => Assisted(
                    "Align the referencing assembly and provider assembly so the referenced member exists.",
                    "Install provider and consumer assemblies built against the same API surface, or restore the package version that exposes the referenced member.",
                    "Full signature compatibility, reflection usage, and runtime load success are not proven."),
                IssueCode.MissingField => Assisted(
                    "Align the referencing assembly and provider assembly so the referenced field exists.",
                    "Install provider and consumer assemblies built against the same field surface, or restore the package version that exposes the referenced field.",
                    "Field compatibility, reflection usage, and runtime load success are not proven."),
                IssueCode.MissingImpl => Assisted(
                    "Rebuild the implementing assembly against the interface surface shipped by the provider.",
                    "Install provider and consumer assemblies built against the same interface surface, or rebuild the implementing class so it implements every interface member.",
                    "Default interface implementations outside the scanned directory and runtime load success are not proven."),
                IssueCode.Inaccessible => Assisted(
                    "Use a public provider API, or ship a provider that grants InternalsVisibleTo to the consumer.",
                    "Rebuild the consumer against the provider's public API, install a provider that grants InternalsVisibleTo, or declare IgnoresAccessChecksTo deliberately.",
                    "Relaxed runtime access checks (Mono) and reflection-based access are not proven."),
                IssueCode.MissingNative => Assisted(
                    "Ship the P/Invoked native library next to the consumer, or rely on a host-installed copy deliberately.",
                    "Install the native library (matching the consumer architecture) into the scanned directory, or confirm the host provides it on its library search path.",
                    "Host library search paths, OS-provided copies, and runtime DllImport resolvers are not observed."),
                IssueCode.ReflectionMissing => Assisted(
                    "Install or restore the assembly named by the reflection load string.",
                    "Install the reflected assembly into the scanned directory or remove the literal reflection load.",
                    "Reflection target API compatibility and runtime load success are not proven."),
                IssueCode.DupProvider => Assisted(
                    "Keep one provider copy for this assembly name.",
                    "Remove or relocate duplicate provider copies in the scanned directory.",
                    "Package ownership and intended provider selection are not proven."),
                _ => ForBep(code)
            };
        }

        private static IssueRepair ForBep(string code)
        {
            return code switch
            {
                IssueCode.BepMissing => Assisted(
                    "Install or restore the missing BepInEx plugin dependency.",
                    "Install the missing BepInEx plugin dependency into the scanned plugins directory.",
                    "Plugin ABI compatibility and runtime chainloader success are not proven."),
                IssueCode.BepCasing => Assisted(
                    BepCasingStep,
                    BepCasingStep,
                    "Plugin ABI compatibility and runtime chainloader success are not proven."),
                IssueCode.BepDuplicateGuid => Assisted(
                    "Keep one BepInEx plugin for this GUID.",
                    "Remove duplicate BepInEx plugins or ask the plugin authors to use distinct GUIDs.",
                    "The intended plugin provider and runtime chainloader selection are not proven."),
                IssueCode.BepVersionMismatch => Assisted(
                    "Install a BepInEx plugin dependency version that satisfies the declared range.",
                    "Install a compatible BepInEx plugin dependency version or update the dependent plugin declaration.",
                    "Plugin ABI compatibility and runtime chainloader success are not proven."),
                IssueCode.PluginUnresolvedChain => Assisted(
                    "Install or restore the missing managed dependency chain for this plugin.",
                    "Run pefix closure <path> --fail-on-unresolved, then install the missing managed dependency into the scanned plugins directory.",
                    "API compatibility and runtime chainloader success are not proven."),
                IssueCode.BepLoaderMismatch => Assisted(
                    "Keep plugins for a single BepInEx generation and runtime flavor in this folder.",
                    "Install plugins built for one BepInEx generation (5 or 6) and one runtime flavor (Mono or IL2CPP) that matches your installed loader; move the others out of the plugins directory.",
                    "Runtime chainloader state is not observed; declared or scanned loader-target metadata does not prove runtime load success."),
                IssueCode.BepIl2CppApi => Assisted(
                    "Use an IL2CPP-compatible plugin build that avoids System.Reflection.Emit.",
                    "Install the plugin variant built for IL2CPP, or ask the plugin author to replace dynamic code generation with IL2CPP-compatible interop.",
                    "Whether the emitting code path executes at runtime is not observed."),
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported issue code.")
            };
        }

        private static IssueRepair Assisted(string hint, string step, string risk)
        {
            return new IssueRepair
            {
                NextSteps = [step],
                Class = RepairClass.AssistedFix,
                RepairHint = hint,
                VerifyCommand = VerifyScan,
                UnverifiedRisks = [risk]
            };
        }
    }
}
