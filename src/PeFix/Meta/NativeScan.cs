namespace PeFix.Meta;

internal static class NativeScan
{
    private static readonly string[] LibExtensions = [".dll", ".so", ".dylib"];

    private static readonly HashSet<string> SystemModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "advapi32", "aspnetcorev2_inprocess", "bcrypt", "c", "cfgmgr32", "comctl32", "comdlg32", "coreclr",
        "CoreFoundation", "credui", "crypt32", "d3d11", "dbghelp", "dinput8", "dl",
        "dsound", "dwmapi", "dxgi", "gdi32", "glu32", "hid", "hostfxr", "hostpolicy", "httpapi",
        "imm32", "iphlpapi", "kernel32", "kernelbase", "m", "mscoree", "msvcrt",
        "ncrypt", "netapi32", "ntdll", "objc", "ole32", "oleaut32", "opengl32",
        "pdh", "powrprof", "propsys", "psapi", "pthread", "qcall", "rpcrt4", "rt", "secur32",
        "Security", "setupapi", "shcore", "shell32", "shlwapi", "System", "ucrtbase",
        "user32", "userenv", "uxtheme", "version", "winhttp", "wininet", "winmm",
        "winspool", "wintrust", "winusb", "wlanapi", "ws2_32", "wtsapi32", "__Internal"
    };

    public static NativeGap[] FindNativeGaps(IReadOnlyList<Inspection> results, string directory)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(directory);

        Dictionary<string, string> filesByKey = CollectFileKeys(directory);
        Dictionary<string, Inspection> nativeByPath = CollectNativeInspections(results);
        List<NativeGap> gaps = [];
        foreach (Inspection consumer in results)
        {
            if (!consumer.HasCliHeader || consumer.PInvokeDeps is not { Length: > 0 } modules)
                continue;

            foreach (string module in modules)
                AddModuleGap(gaps, module, consumer, filesByKey, nativeByPath);
        }

        return [.. gaps.DistinctBy(item => (
            item.ConsumerPath,
            item.ModuleName,
            item.PresentPath,
            item.PresentMachine,
            item.RequiredMachine))];
    }

    internal static string ModuleKey(string name)
    {
        string baseName = Path.GetFileName(name);
        string? extension = LibExtensions.FirstOrDefault(ext => baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        if (extension is not null)
            baseName = baseName[..^extension.Length];

        if (baseName.StartsWith("lib", StringComparison.OrdinalIgnoreCase) && baseName.Length > 3)
            baseName = baseName[3..];

        return baseName.ToLowerInvariant();
    }

    internal static bool IsSystemModule(string module)
    {
        string baseName = Path.GetFileName(module);
        if (baseName.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)
            || baseName.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase))
            return true;

        return SystemModules.Contains(ModuleKey(module));
    }

    // An absolute-path P/Invoke target names a fixed OS/system location, not a folder dep.
    internal static bool IsAbsolutePath(string module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return module.StartsWith('/')
            || module.StartsWith('\\')
            || (module.Length >= 2 && char.IsAsciiLetter(module[0]) && module[1] == ':');
    }

    private static void AddModuleGap(
        List<NativeGap> gaps,
        string module,
        Inspection consumer,
        Dictionary<string, string> filesByKey,
        Dictionary<string, Inspection> nativeByPath)
    {
        if (IsSystemModule(module) || IsAbsolutePath(module))
            return;

        if (!filesByKey.TryGetValue(ModuleKey(module), out string? presentPath))
        {
            gaps.Add(new NativeGap(module, consumer.Path, null, null, null));
            return;
        }

        if (RequiredMachine(consumer) is not { } required)
            return;

        if (!nativeByPath.TryGetValue(presentPath, out Inspection native) || native.Machine is not { } present)
            return;

        if (!string.Equals(present, required, StringComparison.Ordinal))
            gaps.Add(new NativeGap(module, consumer.Path, presentPath, present, required));
    }

    private static string? RequiredMachine(Inspection consumer)
    {
        if (string.Equals(consumer.PeFormat, "PE32+", StringComparison.Ordinal))
            return consumer.Machine;

        return consumer.ManagedCorFlags.Required32Bit ? "I386" : null;
    }

    private static Dictionary<string, string> CollectFileKeys(string directory)
    {
        Dictionary<string, string> filesByKey = new(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            string key = ModuleKey(path);
            if (key.Length > 0)
                filesByKey.TryAdd(key, path);
        }

        return filesByKey;
    }

    private static Dictionary<string, Inspection> CollectNativeInspections(IReadOnlyList<Inspection> results)
    {
        Dictionary<string, Inspection> nativeByPath = new(StringComparer.Ordinal);
        foreach (Inspection item in results)
        {
            if (item.ValidPe && !item.HasCliHeader)
                nativeByPath.TryAdd(item.Path, item);
        }

        return nativeByPath;
    }
}
