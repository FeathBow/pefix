using System.Reflection.Metadata;

namespace PeFix.Meta;

internal static class BepReader
{
    private const string BepNamespace = "BepInEx";
    private const string RangeName = "VersionRange";
    private const string FlagsName = "Flags";
    private const string PluginShort = "BepInPlugin";
    private const string PluginAttr = "BepInPluginAttribute";
    private const string DepShort = "BepInDependency";
    private const string DepAttr = "BepInDependencyAttribute";
    private const int SoftFlag = 2;
    private static readonly AttrName PluginName = new(PluginShort, PluginAttr);
    private static readonly AttrName DepName = new(DepShort, DepAttr);

    public static BepInfo? Read(MetadataReader reader)
    {
        List<BepPlugin> plugins = [];
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            BepPlugin? plugin = ReadPlugin(reader, type);
            if (plugin.HasValue)
                plugins.Add(plugin.Value);
        }

        return plugins.Count == 0 ? null : new BepInfo([.. plugins]);
    }

    private static BepPlugin? ReadPlugin(MetadataReader reader, TypeDefinition type)
    {
        CustomAttributeHandleCollection attrs = type.GetCustomAttributes();
        foreach (CustomAttributeHandle handle in attrs)
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (!IsBepType(reader, attr, PluginName))
                continue;

            BepPlugin? plugin = TryPlugin(attr, ReadDeps(reader, attrs));
            if (plugin.HasValue)
                return plugin;
        }

        return null;
    }

    private static BepPlugin? TryPlugin(CustomAttribute attr, BepDep[] deps)
    {
        try
        {
            string? guid = AttrReader.ReadFixedString(attr, 0);
            string? name = AttrReader.ReadFixedString(attr, 1);
            string? version = AttrReader.ReadFixedString(attr, 2);
            return guid is { Length: > 0 } && name is { Length: > 0 } && version is { Length: > 0 }
                ? new BepPlugin(guid, name, version, deps)
                : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static BepDep[] ReadDeps(MetadataReader reader, CustomAttributeHandleCollection attrs)
    {
        List<BepDep> deps = [];
        foreach (CustomAttributeHandle handle in attrs)
        {
            CustomAttribute attr = reader.GetCustomAttribute(handle);
            if (IsBepType(reader, attr, DepName) && TryDep(attr) is { } dep)
                deps.Add(dep);
        }

        return [.. deps];
    }

    private static BepDep? TryDep(CustomAttribute attr)
    {
        try
        {
            CustomAttributeValue<object?> decoded = attr.DecodeValue(AttrTypes.Instance);
            if (decoded.FixedArguments.Length == 0)
                return null;

            if (decoded.FixedArguments[0].Value is not string guid || guid.Length == 0)
                return null;

            DepArgs args = ReadDepArgs(decoded);
            return new BepDep(guid, args.Range, args.Hard);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static DepArgs ReadDepArgs(CustomAttributeValue<object?> decoded)
    {
        string? range = AttrReader.ReadNamedString(decoded, RangeName);
        int? flags = AttrReader.ReadNamedInt(decoded, FlagsName);
        if (decoded.FixedArguments.Length > 1)
        {
            object? second = decoded.FixedArguments[1].Value;
            range ??= second as string;
            if (second is int fixedFlags)
                flags ??= fixedFlags;
        }

        return new DepArgs(range, flags != SoftFlag);
    }

    private static bool IsBepType(MetadataReader reader, CustomAttribute attr, AttrName name)
    {
        return AttrReader.IsMatch(reader, attr, BepNamespace, name.Short)
            || AttrReader.IsMatch(reader, attr, BepNamespace, name.Full);
    }

    private readonly record struct AttrName(string Short, string Full);

    private readonly record struct DepArgs(string? Range, bool Hard);
}
