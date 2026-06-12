namespace PeFix.Meta;

/// <summary>
/// The loader a plugin targets: its structural generation, runtime flavor, and
/// the parsed build <see cref="LoaderVersion"/> of the BepInEx loader assembly
/// it links. Generation and flavor are the hard load-blocking partitions;
/// <see cref="LoaderVersion"/> is the open-ended semantic-version axis for
/// build-drift checks (BepInEx 6 bleeding-edge builds are not interchangeable).
/// </summary>
public readonly record struct LoaderTarget(
    LoaderGeneration Generation,
    LoaderFlavor Flavor,
    Version? LoaderVersion = null,
    string? ReferenceName = null)
{
    /// <summary>An assembly that references no BepInEx loader surface.</summary>
    public static readonly LoaderTarget None = new(LoaderGeneration.Unknown, LoaderFlavor.Unknown);

    /// <summary>True when the assembly references a recognizable BepInEx loader generation.</summary>
    public bool IsBepInExTarget => Generation != LoaderGeneration.Unknown;

    /// <summary>The loader assembly name and version that proved this target, e.g. "BepInEx.Core 6.0.0.0".</summary>
    public string? Reference
    {
        get
        {
            if (ReferenceName is null)
                return null;
            return LoaderVersion is null ? ReferenceName : $"{ReferenceName} {LoaderVersion}";
        }
    }

    /// <summary>
    /// True unless two known generations or two known flavors disagree. An
    /// unknown generation or flavor never forces an incompatibility, so a
    /// BepInEx.Core-only plugin stays compatible with either flavor. Build
    /// version drift is intentionally not a hard incompatibility here.
    /// </summary>
    public bool IsCompatibleWith(LoaderTarget other)
    {
        bool generationsClash = Generation != LoaderGeneration.Unknown
            && other.Generation != LoaderGeneration.Unknown
            && Generation != other.Generation;
        bool flavorsClash = Flavor != LoaderFlavor.Unknown
            && other.Flavor != LoaderFlavor.Unknown
            && Flavor != other.Flavor;
        return !generationsClash && !flavorsClash;
    }
}
