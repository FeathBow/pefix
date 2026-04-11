using System.Reflection;

namespace PeFix.Patch;

internal static class Validator
{
    public static void Validate(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            _ = AssemblyName.GetAssemblyName(fullPath);
        }
        catch (BadImageFormatException ex)
        {
            throw new InvalidOperationException($"Patched assembly manifest {fullPath} could not be read by the CLR.", ex);
        }
        catch (FileLoadException ex)
        {
            throw new InvalidOperationException($"Patched assembly manifest {fullPath} could not be read by the CLR.", ex);
        }
    }
}
