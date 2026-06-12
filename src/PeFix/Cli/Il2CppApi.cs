using PeFix.Meta;

namespace PeFix.Cli;

internal static class Il2CppApi
{
    private const string EmitAssembly = "System.Reflection.Emit";
    private const string EmitPrefix = "System.Reflection.Emit.";

    public static DirectoryIssue[] Explain(MismatchCtx input)
    {
        LoaderTarget host = input.DeclaredHost is { IsBepInExTarget: true } declared
            ? declared
            : LoaderMismatchExplain.DetectHost(input.Results);
        if (host.Flavor != LoaderFlavor.Il2Cpp)
            return [];

        List<DirectoryIssue> issues = [];
        foreach (Inspection result in input.Results)
        {
            if (result.BepInEx is not { Plugins.Length: > 0 })
                continue;

            if (!UsesEmit(result))
                continue;

            string file = input.Rel.RelativePath(result.Path);
            issues.Add(RepairGuide.ForIssue(
                IssueCode.BepIl2CppApi,
                "System.Reflection.Emit",
                $"{file} uses System.Reflection.Emit, which the IL2CPP runtime does not support; dynamic code generation throws PlatformNotSupportedException.",
                [file]));
        }

        return [.. issues];
    }

    private static bool UsesEmit(Inspection result)
    {
        if (HasEmitAssemblyRef(result))
            return true;

        if (result.View is not { } view)
            return false;

        return view.MethodRefs.Any(item => IsEmitType(item.TypeName))
            || view.FieldRefs.Any(item => IsEmitType(item.TypeName));
    }

    private static bool HasEmitAssemblyRef(Inspection result)
    {
        return result.AssemblyReferences is { } references
            && references.Any(item => string.Equals(item.Name, EmitAssembly, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEmitType(string typeName)
    {
        return typeName.StartsWith(EmitPrefix, StringComparison.Ordinal);
    }
}
