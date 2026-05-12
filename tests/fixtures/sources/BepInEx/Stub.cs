namespace BepInEx;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class BepInPlugin : Attribute
{
    public BepInPlugin(string guid, string name, string version)
    {
        GUID = guid;
        Name = name;
        Version = version;
    }

    public string GUID { get; }

    public string Name { get; }

    public string Version { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class BepInDependency : Attribute
{
    public BepInDependency(string guid)
    {
        DependencyGUID = guid;
        Flags = DependencyFlags.HardDependency;
    }

    public BepInDependency(string guid, DependencyFlags flags)
    {
        DependencyGUID = guid;
        Flags = flags;
    }

    public BepInDependency(string guid, string range)
    {
        DependencyGUID = guid;
        VersionRange = range;
        Flags = DependencyFlags.HardDependency;
    }

    public string DependencyGUID { get; }

    public string? VersionRange { get; set; }

    public DependencyFlags Flags { get; set; }

    public enum DependencyFlags
    {
        HardDependency = 1,
        SoftDependency = 2
    }
}
