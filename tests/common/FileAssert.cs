namespace PeFix.Tests;

internal static class FileAssert
{
    public static byte[] ReadBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    public static void Unchanged(byte[] expected, string path)
    {
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    public static void Changed(byte[] expected, string path)
    {
        Assert.NotEqual(expected, File.ReadAllBytes(path));
    }
}
