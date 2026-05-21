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

    public static DirIssue ForIssue(IssueFacts issue)
    {
        var repair = IssueRepair.For(issue.Code);
        return new DirIssue(
            issue.Code,
            issue.Subject,
            issue.Summary,
            issue.Files,
            repair.NextSteps,
            repair.Class,
            repair.RepairHint,
            repair.VerifyCommand,
            repair.UnverifiedRisks);
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

    internal sealed class IssueFacts
    {
        public required string Code { get; init; }
        public required string Subject { get; init; }
        public required string Summary { get; init; }
        public required string[] Files { get; init; }
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
                IssueCode.DupProvider => Assisted(
                    "Keep one provider copy for this assembly name.",
                    "Remove or relocate duplicate provider copies in the scanned directory.",
                    "Package ownership and intended provider selection are not proven."),
                IssueCode.BepMissing => Assisted(
                    "Install or restore the missing BepInEx plugin dependency.",
                    "Install the missing BepInEx plugin dependency into the scanned plugins directory.",
                    "Plugin ABI compatibility and runtime chainloader success are not proven."),
                IssueCode.BepCasing => Assisted(
                    BepCasingStep,
                    BepCasingStep,
                    "Plugin ABI compatibility and runtime chainloader success are not proven."),
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
