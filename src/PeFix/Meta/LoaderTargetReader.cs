namespace PeFix.Meta;

/// <summary>
/// Derives the <see cref="LoaderTarget"/> a plugin was built against from the
/// plugin's own assembly references. This is intentionally install-free: the
/// generation and flavor are decided purely from which BepInEx surface the
/// plugin links, so a single distributed DLL can be classified before any host
/// is present.
/// </summary>
public static class LoaderTargetReader
{
    private const string Bep5Monolithic = "BepInEx";
    private const string Bep6Core = "BepInEx.Core";
    private const string Bep6UnityMono = "BepInEx.Unity.Mono";
    private const string Bep6UnityIl2Cpp = "BepInEx.Unity.IL2CPP";
    private const string Bep6Il2Cpp = "BepInEx.IL2CPP";
    private const string Il2CppInteropPrefix = "Il2CppInterop";

    public static LoaderTarget FromReferences(IReadOnlyList<AssemblyIdentity>? references)
    {
        if (references is null || references.Count == 0)
            return LoaderTarget.None;

        AssemblyIdentity? il2cpp = null;
        AssemblyIdentity? mono6 = null;
        AssemblyIdentity? core6 = null;
        AssemblyIdentity? monolithic5 = null;

        foreach (AssemblyIdentity reference in references)
        {
            string name = reference.Name;
            if (Eq(name, Bep6UnityIl2Cpp) || Eq(name, Bep6Il2Cpp)
                || name.StartsWith(Il2CppInteropPrefix, StringComparison.OrdinalIgnoreCase))
                il2cpp ??= reference;
            else if (Eq(name, Bep6UnityMono))
                mono6 ??= reference;
            else if (Eq(name, Bep6Core))
                core6 ??= reference;
            else if (Eq(name, Bep5Monolithic))
                monolithic5 ??= reference;
        }

        // Precedence: flavor-specific BepInEx 6 ref is strongest; BepInEx.Core alone fixes
        // generation not flavor; monolithic BepInEx is BepInEx 5 (Mono only). Build version from
        // BepInEx.Core in preference to flavor shim.
        if (il2cpp is { } il2cppRef)
            return Make(LoaderGeneration.BepInEx6, LoaderFlavor.Il2Cpp, core6 ?? il2cppRef);
        if (mono6 is { } mono6Ref)
            return Make(LoaderGeneration.BepInEx6, LoaderFlavor.Mono, core6 ?? mono6Ref);
        if (core6 is { } core6Ref)
            return Make(LoaderGeneration.BepInEx6, LoaderFlavor.Unknown, core6Ref);
        if (monolithic5 is { } monolithic5Ref)
            return Make(LoaderGeneration.BepInEx5, LoaderFlavor.Mono, monolithic5Ref);

        return LoaderTarget.None;
    }

    public static IReadOnlyDictionary<string, LoaderTarget> FromInspections(IReadOnlyList<Inspection> inspections)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        Dictionary<string, LoaderTarget> byPath = new(StringComparer.Ordinal);
        foreach (Inspection inspection in inspections)
            byPath[inspection.Path] = FromReferences(inspection.AssemblyReferences);

        return byPath;
    }

    private static LoaderTarget Make(LoaderGeneration generation, LoaderFlavor flavor, AssemblyIdentity reference)
    {
        Version? version = Version.TryParse(reference.Version, out Version? parsed) ? parsed : null;
        return new LoaderTarget(generation, flavor, version, reference.Name);
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
